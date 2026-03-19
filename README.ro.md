# SmartAuto – Ghid pas cu pas pentru rularea aplicației (Română)

> **SmartAuto** este o unealtă modernă de automatizare a acțiunilor pe desktop-ul Windows, concepută pentru utilizatori non-tehnici. Acest ghid explică, pas cu pas, tot ce trebuie să faci pentru a porni și a folosi aplicația.

---

## Cuprins

1. [Cerințe de sistem](#1-cerințe-de-sistem)
2. [Instalarea uneltelor necesare](#2-instalarea-uneltelor-necesare)
3. [Descărcarea codului sursă](#3-descărcarea-codului-sursă)
4. [Restaurarea pachetelor NuGet](#4-restaurarea-pachetelor-nuget)
5. [Compilarea soluției](#5-compilarea-soluției)
6. [Rularea aplicației](#6-rularea-aplicației)
7. [Rularea testelor automate](#7-rularea-testelor-automate)
8. [Utilizarea aplicației SmartAuto](#8-utilizarea-aplicației-smartauto)
9. [Rezolvarea problemelor frecvente](#9-rezolvarea-problemelor-frecvente)

---

## 1. Cerințe de sistem

Înainte de a începe, asigură-te că sistemul tău îndeplinește **toate** cerințele de mai jos:

| Cerință | Detalii |
|---|---|
| **Sistem de operare** | Windows 10 versiunea 1809 (build 17763) sau mai nou; Windows 11 recomandat |
| **Arhitectură** | Exclusiv **x64** (64 de biți) |
| **Memorie RAM** | Minimum 4 GB (8 GB recomandat) |
| **Spațiu pe disc** | Minimum 5 GB liberi |
| **Conexiune internet** | Necesară la primul build (descărcare pachete NuGet) |
| **Drepturi de administrator** | Necesare pentru instalarea uneltelor și rularea hook-urilor globale |

---

## 2. Instalarea uneltelor necesare

### Pasul 2.1 – Instalează .NET 8 SDK

1. Deschide browserul și mergi la: **https://dotnet.microsoft.com/download/dotnet/8.0**
2. Click pe **"Download .NET 8.0 SDK"** – alege varianta **Windows x64**.
3. Rulează programul de instalare descărcat (ex.: `dotnet-sdk-8.0.xxx-win-x64.exe`).
4. Urmează pașii din instalator (Next → Next → Install).
5. După finalizare, verifică instalarea: deschide **Command Prompt** (cmd) sau **PowerShell** și tastează:
   ```
   dotnet --version
   ```
   Trebuie să apară un număr de versiune care începe cu `8.0`.

### Pasul 2.2 – Instalează Visual Studio 2022 (recomandat)

> Alternativa fără Visual Studio este descrisă la **Pasul 2.3**.

1. Descarcă **Visual Studio 2022 Community** (gratuit) de la: **https://visualstudio.microsoft.com/vs/**
2. Rulează instalatorul.
3. La ecranul de selectare a componentelor (**Workloads**), bifează obligatoriu:
   - ✅ **".NET Desktop Development"**
   - ✅ **"Windows application development"** (include Windows App SDK și WinUI 3)
4. Din tab-ul **"Individual components"**, adaugă:
   - ✅ **Windows 11 SDK (10.0.22621.0)** sau mai recent
5. Click **Install** și așteaptă finalizarea (poate dura 15-30 minute).

### Pasul 2.3 – Alternativă: doar .NET 8 SDK + Windows App SDK (fără Visual Studio)

Dacă nu dorești Visual Studio, instalează **Windows App SDK Runtime** separat:

1. Mergi la: **https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads**
2. Descarcă și instalează **Windows App SDK 1.6** (versiunea Runtime x64).

### Pasul 2.4 – Instalează Git

1. Descarcă Git de la: **https://git-scm.com/download/win**
2. Rulează instalatorul cu opțiunile implicite.
3. Verifică: deschide Command Prompt și tastează:
   ```
   git --version
   ```

---

## 3. Descărcarea codului sursă

1. Deschide **Command Prompt** sau **PowerShell** ca **Administrator**.
2. Navighează în folderul unde dorești să descarci proiectul, de exemplu:
   ```
   cd C:\Proiecte
   ```
3. Clonează repository-ul:
   ```
   git clone https://github.com/Mymady10/Marius7654635.git
   ```
4. Intră în folderul proiectului:
   ```
   cd Marius7654635
   ```
5. Verifică că fișierul soluției există:
   ```
   dir SmartAuto.sln
   ```
   Trebuie să apară fișierul `SmartAuto.sln` în lista afișată.

---

## 4. Restaurarea pachetelor NuGet

Înainte de compilare, toate dependențele (bibliotecile externe) trebuie descărcate automat.

1. În aceeași fereastră Command Prompt / PowerShell, din folderul `Marius7654635`, rulează:
   ```
   dotnet restore SmartAuto.sln
   ```
2. Așteaptă finalizarea. Vei vedea mesaje de tipul:
   ```
   Restored SmartAuto\SmartAuto.csproj
   Restored SmartAuto.Domain\SmartAuto.Domain.csproj
   ...
   ```
3. Dacă apare o eroare legată de **credențiale NuGet** sau **surse private**, verifică conexiunea la internet și reîncearcă.

---

## 5. Compilarea soluției

### Varianta A – din linia de comandă (recomandat pentru primul test)

1. Rulează comanda de build:
   ```
   dotnet build SmartAuto.sln --configuration Debug --runtime win-x64
   ```
2. Dacă build-ul a reușit, vei vedea la final:
   ```
   Build succeeded.
       0 Warning(s)
       0 Error(s)
   ```
3. Pentru un build de producție (optimizat), folosește:
   ```
   dotnet build SmartAuto.sln --configuration Release --runtime win-x64
   ```

### Varianta B – din Visual Studio 2022

1. Deschide Visual Studio 2022.
2. Click pe **"Open a project or solution"** → navighează la `C:\Proiecte\Marius7654635\SmartAuto.sln` → **Open**.
3. Visual Studio va detecta automat că trebuie restaurate pachetele NuGet (confirmă dacă este solicitat).
4. Asigură-te că în bara de sus este selectat:
   - **Configuration**: `Debug` (sau `Release` pentru producție)
   - **Platform**: `x64`
5. Apasă **F6** sau mergi la meniu **Build → Build Solution**.
6. Verifică că în panoul **"Output"** apare `Build succeeded`.

---

## 6. Rularea aplicației

> **Important:** SmartAuto folosește hook-uri globale de tastatură și mouse. Pe unele sisteme, este necesară rularea **ca Administrator** pentru a captura evenimentele din toate aplicațiile.

### Varianta A – din linia de comandă

```
dotnet run --project SmartAuto\SmartAuto.csproj --configuration Debug --runtime win-x64
```

### Varianta B – executabilul direct

1. După build, navighează în:
   ```
   SmartAuto\bin\x64\Debug\net8.0-windows10.0.22621.0\win-x64\
   ```
2. Rulează (dublu-click sau din cmd):
   ```
   SmartAuto.exe
   ```

### Varianta C – din Visual Studio 2022

1. În **Solution Explorer**, click dreapta pe proiectul **SmartAuto** → **"Set as Startup Project"**.
2. Apasă **F5** (cu debugger) sau **Ctrl+F5** (fără debugger).
3. Aplicația SmartAuto va porni și va afișa fereastra principală.

### Ce vei vedea la pornire

La prima pornire, aplicația:
- Creează un folder de loguri la: `%LOCALAPPDATA%\SmartAuto\Logs\`
- Afișează fereastra principală cu meniu lateral de navigare (NavigationView) cu secțiunile:
  - **Record** – înregistrarea acțiunilor
  - **Script Editor** – editarea scripturilor
  - **Run & Debug** – rularea și depanarea
  - **Library** – biblioteca de scripturi salvate
  - **Settings** – setările aplicației

---

## 7. Rularea testelor automate

Testele verifică că toate componentele funcționează corect.

1. Din linia de comandă, din folderul `Marius7654635`:
   ```
   dotnet test SmartAuto.Tests\SmartAuto.Tests.csproj --configuration Debug --runtime win-x64 --verbosity normal
   ```
2. La final vei vedea un raport de tipul:
   ```
   Passed!  - Failed: 0, Passed: XX, Skipped: 0
   ```
3. Dacă vrei și raport de **acoperire a codului** (code coverage):
   ```
   dotnet test SmartAuto.Tests\SmartAuto.Tests.csproj --collect:"XPlat Code Coverage"
   ```

---

## 8. Utilizarea aplicației SmartAuto

### 8.1 Înregistrarea unei automatizări (Record)

1. Click pe **"Record"** în meniul lateral stâng.
2. Apasă butonul mare **"Start Recording"** (sau folosește scurtătura globală **Ctrl+Alt+R**).
3. SmartAuto va înregistra în fundal toate click-urile de mouse și apăsările de taste.
4. Efectuează acțiunile pe care vrei să le automatizezi (ex.: deschide o aplicație, completează un formular).
5. Apasă din nou **"Stop Recording"** (sau **Ctrl+Alt+R**).
6. Acțiunile înregistrate vor apărea în lista din panoul central.

### 8.2 Editarea scriptului (Script Editor)

1. Click pe **"Script Editor"** în meniu.
2. Poți vizualiza, reordona, edita sau șterge fiecare acțiune înregistrată.
3. Poți adăuga acțiuni speciale: căutare text pe ecran, așteptare condiție, bucle, variabile dinamice.
4. Salvează scriptul cu butonul **"Save"** – fișierul va fi stocat în format JSON versioned.

### 8.3 Rularea unui script (Run & Debug)

1. Click pe **"Run & Debug"** în meniu.
2. Selectează scriptul dorit din listă.
3. Apasă **"Run"** pentru rulare completă sau **"Step"** pentru rulare pas cu pas.
4. Folosește scurtătura **Ctrl+Alt+P** pentru a opri/relua redarea.
5. Urmărește progresul și log-urile în panoul de jos al ferestrei.

### 8.4 Biblioteca de scripturi (Library)

1. Click pe **"Library"** pentru a vedea toate scripturile salvate.
2. Poți importa/exporta scripturi ca fișiere JSON.
3. Poți rula direct orice script din bibliotecă.

### 8.5 Setări (Settings)

1. Click pe **"Settings"** (pictograma roată din josul meniului).
2. Configurează:
   - Întârzierea minimă între acțiuni (implicit 100 ms)
   - Pragul de confidence pentru detectarea elementelor
   - Activarea/dezactivarea telemetriei (implicit dezactivată)
   - Tema vizuală (Light / Dark / High Contrast – urmărește automat setarea Windows)

---

## 9. Rezolvarea problemelor frecvente

| Problemă | Soluție |
|---|---|
| **"Build failed – SDK not found"** | Reinstalează .NET 8 SDK și repornește calculatorul. |
| **"Windows App SDK not found"** | Instalează Windows App SDK Runtime de la linkul din Pasul 2.3. |
| **Aplicația nu pornește (eroare la hook-uri)** | Rulează `SmartAuto.exe` ca Administrator (click dreapta → "Run as administrator"). |
| **Nu se înregistrează click-urile în alte aplicații** | Rulează SmartAuto ca Administrator. Unele aplicații UAC Elevated blochează hook-urile non-elevate. |
| **Eroare "single instance" – aplicația nu pornește** | O altă instanță SmartAuto rulează deja. Închide-o din Task Manager (Ctrl+Shift+Esc → caută SmartAuto → End Task). |
| **Log-urile unde se găsesc?** | La: `%LOCALAPPDATA%\SmartAuto\Logs\` (scrie în bara de adrese din Explorer). |
| **Testele eșuează cu "platform not supported"** | Asigură-te că rulezi testele pe **x64** (`--runtime win-x64`) și pe Windows 10/11. |
| **NuGet restore eșuează (timeout)** | Verifică conexiunea la internet. Încearcă: `dotnet restore --no-cache`. |

---

## Structura proiectului (referință rapidă)

```
SmartAuto.sln
├── SmartAuto/              ← Aplicația WinUI 3 (executabilul principal)
├── SmartAuto.Abstractions/ ← Interfețe și contracte
├── SmartAuto.Domain/       ← Logica de business (acțiuni, scripturi, modele)
├── SmartAuto.Application/  ← Orchestrarea serviciilor
├── SmartAuto.Infrastructure/ ← Hook-uri, motoare de selecție, playback, OCR
├── SmartAuto.Common/       ← Constante, extensii utilitare
├── SmartAuto.Tests/        ← Teste automate (xUnit)
└── docs/                   ← Documentație (model de securitate STRIDE)
```

---

## Scurtături de tastatură

| Scurtătură | Acțiune |
|---|---|
| **Ctrl+Alt+R** | Pornește / oprește înregistrarea |
| **Ctrl+Alt+P** | Pornește / pune pe pauză redarea scriptului |

---

> **Notă de securitate:** SmartAuto nu stochează niciodată text sensibil (parole, date personale) în loguri sau scripturi. Tastele sensibile sunt mascate automat (`<masked>`). Telemetria este **dezactivată implicit** și poate fi activată exclusiv cu consimțământul utilizatorului.
