# Prompt — Système de traitement automatisé des Notes de Frais

> **Instructions** : Colle ce prompt tel quel dans une nouvelle conversation Claude. Il contient toutes les spécifications pour générer le système complet en une seule passe.

---

Tu es un architecte logiciel senior spécialisé .NET et Azure. Tu dois concevoir et coder **intégralement** un système de traitement automatisé de notes de frais de restauration. Le système est une **Console App .NET 10** en **Clean Architecture** qui :

1. Scanne un dossier local (synchronisé OneDrive) contenant des photos (JPG/PNG) et des PDF de tickets de restaurant
2. Envoie chaque image/page à **Azure Document Intelligence** (`prebuilt-receipt`) pour extraction OCR
3. Gère les **PDF multi-tickets** : détecte chaque ticket individuellement via les bounding boxes retournées par l'API, rasterise la page, et crop chaque ticket en image individuelle
4. Extrait de chaque ticket : **date, heure, nom du restaurant, montant HT, TVA, montant TTC**
5. Insère les données dans un **fichier Excel existant** (fourni par un comptable) via **ClosedXML**
6. Renomme et déplace les fichiers traités dans un sous-dossier organisé par mois
7. Gère les erreurs, doublons, et produit un récapitulatif de run

---

## 1. Architecture du projet — Clean Architecture

```
src/
├── NdfProcessor.Domain/            # Entités, interfaces, value objects
│   ├── Entities/
│   │   ├── Receipt.cs              # Date, Heure, Restaurant, MontantHT, TVA, MontantTTC
│   │   ├── ProcessingResult.cs     # Résultat du traitement d'un fichier
│   │   └── RunSummary.cs           # Récap du run (traités, erreurs, doublons, RunId)
│   ├── Interfaces/
│   │   ├── IOcrService.cs          # Extraction OCR
│   │   ├── IExcelService.cs        # Lecture/écriture Excel
│   │   ├── IFileService.cs         # Opérations fichiers (lecture, déplacement, renommage)
│   │   ├── IPdfService.cs          # Rasterisation PDF, crop par bounding box
│   │   └── IDuplicateDetector.cs   # Détection de doublons
│   └── Enums/
│       └── ProcessingStatus.cs     # Success, Error, Duplicate
│
├── NdfProcessor.Infrastructure/     # Implémentations concrètes
│   ├── Services/
│   │   ├── AzureOcrService.cs      # Appel Azure Document Intelligence REST API
│   │   ├── ClosedXmlExcelService.cs # Manipulation Excel avec ClosedXML
│   │   ├── LocalFileService.cs     # Gestion fichiers système local
│   │   ├── PdfService.cs           # Rasterisation avec SkiaSharp + crop bounding box
│   │   └── DuplicateDetector.cs    # Détection doublons (date+heure+montant+restaurant)
│   └── Configuration/
│       └── AppSettings.cs          # Mapping du appsettings.json (strongly typed)
│
├── NdfProcessor.Application/        # Use cases / orchestration
│   ├── UseCases/
│   │   └── ProcessReceiptsUseCase.cs # Orchestration principale du pipeline
│   └── DTOs/
│       └── OcrResult.cs            # DTO retour OCR
│
├── NdfProcessor.Console/            # Point d'entrée
│   ├── Program.cs                  # Setup DI, configuration, lancement
│   └── appsettings.json            # Configuration complète
│
tests/
├── NdfProcessor.Domain.Tests/
├── NdfProcessor.Infrastructure.Tests/
└── NdfProcessor.Application.Tests/
```

Chaque projet a ses propres dépendances NuGet. Les références inter-projets suivent la règle Clean Architecture :
- **Domain** : aucune dépendance externe
- **Application** : référence Domain
- **Infrastructure** : référence Domain et Application
- **Console** : référence tout

---

## 2. Configuration — `appsettings.json`

Toute la configuration est externalisée et **100% configurable** :

```json
{
  "AzureOcr": {
    "Endpoint": "https://<resource-name>.cognitiveservices.azure.com/",
    "ApiKey": "<api-key>",
    "ModelId": "prebuilt-receipt"
  },
  "Paths": {
    "InputFolder": "C:\\Users\\<user>\\OneDrive\\NoteDeFrais\\ATraiter",
    "ErrorFolder": "C:\\Users\\<user>\\OneDrive\\NoteDeFrais\\Erreurs",
    "ProcessedFolderRoot": "C:\\Users\\<user>\\OneDrive\\NoteDeFrais\\Traités",
    "ExcelFilePath": "C:\\Users\\<user>\\OneDrive\\Comptabilite\\{year}.xlsx",
    "ErrorReportFolder": "C:\\Users\\<user>\\OneDrive\\NoteDeFrais\\Rapports"
  },
  "Excel": {
    "SheetNameFormat": "{month:D2}",
    "StartRow": 2,
    "Columns": {
      "Date": "A",
      "Heure": "B",
      "Restaurant": "C",
      "MontantHT": "D",
      "TVA": "E",
      "MontantTTC": "F"
    }
  },
  "Processing": {
    "SupportedImageExtensions": [ ".jpg", ".jpeg", ".png" ],
    "SupportedPdfExtensions": [ ".pdf" ],
    "ProcessedFileNameFormat": "{date:yyyy-MM-dd}_{montantTTC}_EUR",
    "ProcessedSubFolderFormat": "yyyy-MM"
  }
}
```

**Notes :**
- `{year}` dans `ExcelFilePath` est remplacé dynamiquement par l'année de la facture
- `SheetNameFormat` : les onglets s'appellent `01`, `02`... `12`
- `StartRow` : ligne de début des données (les en-têtes sont au-dessus)
- Les colonnes sont mappées par lettre pour flexibilité totale
- Le nom du fichier Excel est dynamique (un fichier par an)

---

## 3. Pipeline de traitement — `ProcessReceiptsUseCase`

Le flow principal est le suivant :

```
1. Générer un RunId unique (GUID court ou timestamp)
2. Scanner le dossier d'entrée pour trouver tous les fichiers JPG/PNG/PDF
3. Pour chaque fichier :
   a. Si IMAGE (jpg/png) :
      - Envoyer à Azure Document Intelligence (prebuilt-receipt)
      - L'API peut détecter plusieurs tickets sur une même image
      - Pour chaque ticket détecté : extraire les champs
   b. Si PDF :
      - Pour chaque page du PDF :
        - Envoyer la page à Azure Document Intelligence
        - L'API détecte N tickets avec leurs bounding boxes
        - Rasteriser la page en image haute résolution (300 DPI min)
        - Pour chaque ticket détecté : crop via bounding box → image individuelle
        - Extraire les champs de chaque ticket
   c. Pour chaque Receipt extrait :
      - Vérifier les doublons (date + heure + montant + nom restaurant)
        → Si doublon : WARNING en console avec nom du fichier, skip
      - Insérer dans le bon onglet Excel (mois correspondant à la date du ticket)
      - Ajouter la ligne à la première ligne vide après StartRow
   d. Déplacer le fichier source :
      - Si SUCCÈS : vers ProcessedFolderRoot/yyyy-MM/yyyy-MM-dd_montant_EUR.ext
        (si multi-tickets dans un PDF, déplacer les images croppées individuellement)
      - Si ERREUR OCR : vers ErrorFolder + log
4. Générer le fichier rapport d'erreurs (si erreurs présentes) :
   - Nom : ErrorReport_{RunId}.txt
   - Contenu : RunId, date/heure du run, liste des fichiers en erreur avec raison
5. Afficher le récapitulatif en console :
   === Run {RunId} terminé ===
   Factures traitées : X
   Factures en erreur : Y
   Doublons détectés  : Z
   Fichier Excel mis à jour : {chemin}
```

---

## 4. Azure Document Intelligence — `AzureOcrService`

- Utiliser l'API REST v4.0 ou le SDK `Azure.AI.FormRecognizer` (dernière version stable compatible .NET 10)
- Modèle : `prebuilt-receipt`
- Champs à extraire du receipt :
  - `MerchantName` → Nom du restaurant
  - `TransactionDate` → Date
  - `TransactionTime` → Heure
  - `Subtotal` → Montant HT
  - `TotalTax` → TVA
  - `Total` → Montant TTC
- Si `Subtotal` est absent : calculer `MontantHT = Total - TotalTax`
- Si `TotalTax` est absent : calculer `TVA = Total - Subtotal`
- Si seul `Total` est disponible : mettre `Total` dans TTC, les autres à `null` → warning dans le log
- Gérer le retry (3 tentatives avec exponential backoff) en cas d'erreur API transitoire
- Chaque receipt retourné par l'API a un champ `BoundingRegions` qui donne les coordonnées → utiliser pour le crop

---

## 5. Gestion PDF multi-tickets — `PdfService`

- Utiliser **SkiaSharp** pour la rasterisation des pages PDF en images
- Utiliser **PdfPig** pour lire le PDF et extraire les pages
- Flow pour un PDF :
  1. Lire le PDF avec PdfPig
  2. Pour chaque page : rasteriser en image PNG à 300 DPI avec SkiaSharp
  3. Envoyer l'image de la page complète à l'OCR
  4. Pour chaque receipt détecté dans la réponse OCR :
     - Récupérer les `BoundingRegions` (polygone)
     - Calculer le rectangle englobant
     - Crop l'image de la page à ce rectangle (avec une marge de 10px)
     - Sauvegarder le crop comme image individuelle
  5. Chaque image croppée est traitée comme un receipt individuel

---

## 6. Détection des doublons — `DuplicateDetector`

- Avant d'insérer une ligne dans Excel, vérifier si une entrée identique existe déjà
- Critères de comparaison : **Date + Heure + Montant TTC + Nom du restaurant** (comparaison case-insensitive, trim des espaces)
- Si doublon détecté :
  - Afficher un **WARNING** en console : `⚠️ DOUBLON DÉTECTÉ : {nomFichier} — {date} {restaurant} {montantTTC}€`
  - Ne pas insérer dans le Excel
  - Compter dans le récapitulatif
- Charger les données existantes du Excel au début du run pour constituer le cache de comparaison

---

## 7. Gestion des fichiers — `LocalFileService`

- **Fichier traité avec succès** :
  - Créer le sous-dossier `yyyy-MM` sous `ProcessedFolderRoot` si inexistant
  - Renommer le fichier : `{date:yyyy-MM-dd}_{montantTTC}_EUR.{extension}`
  - Si le nom existe déjà (deux tickets le même jour au même montant), ajouter un suffixe `_1`, `_2`, etc.
  - Déplacer dans le sous-dossier
- **Fichier en erreur** :
  - Déplacer dans `ErrorFolder`
  - Garder le nom original
- **Formats supportés** : `.jpg`, `.jpeg`, `.png`, `.pdf` (configurable)

---

## 8. Excel — `ClosedXmlExcelService`

- Utiliser **ClosedXML** (NuGet `ClosedXML`)
- Ouvrir le fichier Excel existant (ne pas le recréer)
- Déterminer l'onglet cible à partir du mois de la date du receipt (onglets nommés `01` à `12`)
- Si le fichier Excel pour l'année n'existe pas → le créer avec les 12 onglets et les en-têtes
- Trouver la première ligne vide à partir de `StartRow`
- Écrire les valeurs dans les colonnes configurées
- Formater les montants en nombre avec 2 décimales
- Formater la date selon le format local français (`dd/MM/yyyy`)
- **Sauvegarder le fichier après chaque facture** pour éviter la perte de données en cas de crash
- Gérer le cas où le fichier Excel est ouvert par un autre programme (retry ou message clair)

---

## 9. Tests unitaires — xUnit + NSubstitute

Créer des tests pour :

### Domain Tests
- `Receipt` : validation des champs, calculs HT/TVA/TTC manquants

### Application Tests
- `ProcessReceiptsUseCase` : mocker tous les services, tester le flow complet
  - Cas nominal : 3 fichiers → 3 insertions Excel
  - Cas erreur OCR : 1 fichier illisible → déplacé en erreur
  - Cas doublon : 2 fichiers identiques → 1 insertion + 1 warning
  - Cas PDF multi-tickets : 1 PDF avec 3 tickets → 3 insertions
  - Cas mixte : combiner tous les cas ci-dessus

### Infrastructure Tests
- `DuplicateDetector` : tester la comparaison (case-insensitive, trim, matching exact)
- `ClosedXmlExcelService` : tester l'insertion dans un fichier Excel temporaire réel
- `LocalFileService` : tester le renommage, la gestion des suffixes, la création de dossiers

Utiliser `NSubstitute` pour les mocks. Chaque test est isolé et ne dépend pas du système de fichiers réel (sauf pour les tests d'intégration Excel et FileService qui utilisent un dossier temporaire nettoyé après chaque test).

---

## 10. Terraform — Provisioning Azure Document Intelligence

### Script Bootstrap (`infrastructure/bootstrap.sh`)

```bash
#!/bin/bash
# Bootstrap : crée le Resource Group + Storage Account pour le state Terraform
# Variables configurables
```

Le script doit :
1. Accepter des **variables configurables** : nom du RG, nom du storage account, région, nom du container
2. Région par défaut : `westeurope`
3. Vérifier si le RG existe déjà, le **créer uniquement s'il n'existe pas**
4. Créer le Storage Account pour le state Terraform
5. Créer le container blob pour le state
6. Afficher les valeurs à reporter dans le `backend.tf`

### Terraform (`infrastructure/terraform/`)

Fichiers à générer :

- **`variables.tf`** : toutes les variables configurables (nom ressource, RG, région, SKU, tags)
- **`main.tf`** : Resource Group (data ou resource selon existence) + Azure Cognitive Services Account avec kind `FormRecognizer` et SKU `S0`, model `prebuilt-receipt`
- **`backend.tf`** : backend Azure Storage (valeurs issues du bootstrap)
- **`outputs.tf`** : endpoint et clé API en output (à reporter dans `appsettings.json`)
- **`terraform.tfvars.example`** : exemple de variables

---

## 11. README.md

Générer un README.md complet avec :

1. **Description du projet** : ce que fait le système, à qui il s'adresse
2. **Prérequis** : .NET 10 SDK, Azure CLI, Terraform, compte Azure avec abonnement VS Enterprise
3. **Installation** :
   - Cloner le repo
   - Provisionner l'infra Azure (bootstrap + terraform apply)
   - Reporter endpoint/clé dans `appsettings.json`
   - Configurer les chemins OneDrive locaux
4. **Configuration** : détail de chaque section du `appsettings.json`
5. **Utilisation** : comment lancer l'app, exemples de sortie console
6. **Architecture** : diagramme Clean Architecture, responsabilités de chaque couche
7. **Gestion des cas particuliers** : PDF multi-tickets, doublons, erreurs, fichiers renommés
8. **Tests** : comment lancer les tests (`dotnet test`)
9. **Structure du projet** : arborescence des fichiers

---

## 12. Contraintes techniques non négociables

- **Pas de `Console.WriteLine` brut** : utiliser `ILogger<T>` de `Microsoft.Extensions.Logging` avec les niveaux appropriés (Information, Warning, Error)
- **Injection de dépendances** via `Microsoft.Extensions.DependencyInjection`
- **Configuration** via `Microsoft.Extensions.Configuration` + binding vers des classes POCO
- **Async/await** partout où applicable
- **Nullable reference types** activés
- **Pas de magic strings** : constantes ou configuration
- Les NuGet packages doivent être dans les versions **compatibles .NET 10**
- Chaque fichier `.cs` doit avoir les `using` nécessaires
- Le code doit compiler sans erreur

---

## 13. Livrables attendus

Génère **TOUS** les fichiers suivants, complets et fonctionnels :

1. Fichiers `.csproj` pour chaque projet (avec les NuGet packages)
2. Fichier `NdfProcessor.sln` (solution file)
3. Tous les fichiers C# de chaque couche (Domain, Application, Infrastructure, Console)
4. `appsettings.json` avec des valeurs d'exemple
5. Tous les fichiers de tests
6. `infrastructure/bootstrap.sh`
7. Tous les fichiers Terraform (`main.tf`, `variables.tf`, `backend.tf`, `outputs.tf`, `terraform.tfvars.example`)
8. `README.md`
9. `.gitignore` adapté .NET + Terraform

**Génère le code complet de chaque fichier. Pas de raccourcis, pas de `// TODO`, pas de `...`. Chaque fichier doit être complet et prêt à être copié dans un IDE.**
