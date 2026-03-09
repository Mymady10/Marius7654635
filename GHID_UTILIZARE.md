# Ghid de utilizare — Macro Recorder
### Instrucțiuni pas cu pas în limba română

---

## ⚡ Start Rapid (TL;DR)

Dacă vrei să pornești aplicația cât mai repede, urmează acești **5 pași**:

```bash
# 1. Descarcă proiectul
git clone https://github.com/Mymady10/Marius7654635.git

# 2. Intră în folder
cd Marius7654635

# 3. Instalează dependințele
pip install -r requirements.txt

# 4. Pornește aplicația
python app.py
```

> **5.** Fereastra **Macro Recorder** se deschide. Apasă **F8** pentru a începe înregistrarea. Gata!

Dacă ai nevoie de mai multe detalii, consultă secțiunile de mai jos.

---

## Cuprins

1. [Cerințe preliminare](#1-cerinte-preliminare)
2. [Descărcarea proiectului](#2-descarcarea-proiectului)
3. [Instalarea dependințelor](#3-instalarea-dependintelor)
4. [Pornirea aplicației](#4-pornirea-aplicatiei)
5. [Interfața grafică — prezentare generală](#5-interfata-grafica--prezentare-generala)
6. [Înregistrarea unui macro](#6-inregistrarea-unui-macro)
7. [Redarea unui macro](#7-redarea-unui-macro)
8. [Editarea pașilor înregistrați](#8-editarea-pasilor-inregistrati)
9. [Selectarea unei regiuni de ecran](#9-selectarea-unei-regiuni-de-ecran)
10. [Salvarea și încărcarea unui macro](#10-salvarea-si-incarcarea-unui-macro)
11. [Taste rapide globale](#11-taste-rapide-globale)
12. [Oprirea de urgență](#12-oprirea-de-urgenta)
13. [Probleme frecvente](#13-probleme-frecvente)
14. [Deschiderea paginii web (index.html)](#14-deschiderea-paginii-web-indexhtml)

---

## 1. Cerințe preliminare

Înainte de a rula aplicația, asigură-te că ai instalate pe calculator:

| Cerință | Versiune minimă | Verificare |
|---------|----------------|------------|
| **Python** | 3.9 sau mai nou | `python --version` |
| **pip** | inclusă cu Python | `pip --version` |
| **Windows** | Windows 10 / 11 | _(recomandat)_ |

> **Notă:** Aplicația funcționează și pe Linux sau macOS, dar este optimizată pentru Windows.  
> Pe Linux este necesară biblioteca `python3-xlib` (`sudo apt install python3-xlib`).  
> Pe macOS trebuie acordate permisiuni de **Accesibilitate** în *Preferințe de sistem → Confidențialitate*.

---

## 2. Descărcarea proiectului

### Varianta A — cu Git (recomandat)

Deschide un **terminal** (Command Prompt, PowerShell sau Terminal) și execută:

```bash
git clone https://github.com/Mymady10/Marius7654635.git
cd Marius7654635
```

### Varianta B — descărcare ZIP

1. Accesează pagina GitHub a proiectului:  
   `https://github.com/Mymady10/Marius7654635`
2. Apasă butonul verde **Code → Download ZIP**.
3. Dezarhivează fișierul descărcat.
4. Deschide un terminal și navighează în folderul dezarhivat:
   ```bash
   cd calea\catre\Marius7654635
   ```

---

## 3. Instalarea dependințelor

Aplicația are nevoie de câteva biblioteci Python externe. Instalează-le cu o singură comandă:

```bash
pip install -r requirements.txt
```

Comanda descarcă și instalează automat:

| Bibliotecă | Rol |
|------------|-----|
| `pynput` | Captură și simulare globală de mouse/tastatură |
| `PyAutoGUI` | Automatizare GUI și potrivire de imagini |
| `mss` | Capturi de ecran rapide pe regiuni |
| `Pillow` | Procesare imagini și previzualizare zoom |

> **Sfat:** Dacă primești eroarea `pip: command not found`, încearcă `pip3 install -r requirements.txt` sau `python -m pip install -r requirements.txt`.

---

## 4. Pornirea aplicației

Asigură-te că ești în directorul proiectului, apoi rulează:

```bash
python app.py
```

Fereastra principală a aplicației **Macro Recorder** va apărea pe ecran.

> **Sfat Windows:** Poți crea un fișier `start.bat` cu conținutul `python app.py` și să-l rulezi cu dublu-clic.

---

## 5. Interfața grafică — prezentare generală

```
┌──────────────────────────────────────────────────────────────────┐
│  Macro Settings                                    [ Idle      ] │
│  Name: [Untitled]  Loops: [1] (0=∞)  Speed: [1.0]×  Mode:[normal]│
├──────────────────────────────────────────────────────────────────┤
│ [⏺ Start Recording] [⏹ Stop Recording] [▶ Play] [■ Stop]        │
│ [⊞ Select Region]   [💾 Save]           [📂 Load]               │
├──────────────────────────────────────────────────────────────────┤
│  #  │ Type         │ Coordinates │ Key/Value   │ Delay │ Enabled │
│ ──  │ ──────────── │ ─────────── │ ─────────── │ ───── │ ─────── │
│  1  │ mouse_move   │ (400, 300)  │             │ 0.050 │   ✓     │
│  2  │ mouse_click  │ (400, 300)  │ left        │ 0.020 │   ✓     │
│  3  │ text         │             │ "Salut!"    │ 0.100 │   ✓     │
├──────────────────────────────────────────────────────────────────┤
│ Mouse: (512, 384)  │ Region: none  │ Steps: 3  │ F8=Rec F9=Play  │
└──────────────────────────────────────────────────────────────────┘
```

| Zonă | Descriere |
|------|-----------|
| **Macro Settings** | Setează numele macroului, numărul de repetări, viteza și modul de redare |
| **Butoane de acțiune** | Porni/oprire înregistrare, redare, oprire, selecție regiune, salvare, încărcare |
| **Lista de pași** | Afișează fiecare acțiune înregistrată; poți edita, reordona, activa/dezactiva pași |
| **Bara de stare** | Coordonatele curente ale mouse-ului, region selectată, număr de pași, taste rapide |

---

## 6. Înregistrarea unui macro

Urmează pașii de mai jos pentru a înregistra o secvență de acțiuni:

### Pasul 1 — Pregătirea

1. Deschide aplicația cu `python app.py`.
2. Asigură-te că indicatorul de stare din dreapta sus arată **Idle** (inactiv).
3. Opțional, introdu un nume pentru macro în câmpul **Name**.

### Pasul 2 — Pornirea înregistrării

- **Metodă 1 (buton):** Apasă butonul **⏺ Start Recording** din interfață.
- **Metodă 2 (tastă rapidă):** Apasă **F8** de oriunde de pe calculator.

Indicatorul de stare devine **Recording** (pe fundal roșu) — înregistrarea a început.

### Pasul 3 — Efectuarea acțiunilor

Realizează acțiunile pe care vrei să le automatizezi:

- **Mișcări de mouse** — deplasează cursorul pe ecran
- **Clicuri** — clic stânga, dreapta sau mijloc
- **Scroll** — derulează pagina cu rotița mouse-ului
- **Tastare** — apasă taste sau scrie text

> **Info:** Mișcările minore de mouse (sub 10 pixeli) sunt filtrate automat pentru a păstra fișierele mici. Caracterele tastate consecutiv sunt grupate automat într-un singur pas de tip `text`.

### Pasul 4 — Oprirea înregistrării

- **Metodă 1 (buton):** Apasă **⏹ Stop Recording**.
- **Metodă 2 (tastă rapidă):** Apasă **F8** din nou.

Indicatorul revine la **Idle** și lista de pași se populează cu acțiunile înregistrate.

---

## 7. Redarea unui macro

### Pasul 1 — Configurarea redării

Înainte de a porni redarea, poți ajusta:

| Opțiune | Câmp | Descriere |
|---------|------|-----------|
| **Număr de repetări** | `Loops` | `1` = o singură dată; `0` = buclă infinită; orice număr pozitiv = de atâtea ori |
| **Viteza** | `Speed` | `1.0` = viteză normală; `2.0` = dublu mai rapid; `0.5` = jumătate din viteză |
| **Modul de redare** | `Mode` | `normal` / `fast` / `humanized` |

**Moduri de redare:**
- `normal` — redă exact întârzierile din înregistrare.
- `fast` — limitează întârzierile la maximum 50 ms (mult mai rapid).
- `humanized` — adaugă variație aleatoare (±20%) și mișcă mouse-ul pe o curbă lină, pentru comportament mai natural.

### Pasul 2 — Pornirea redării

- **Metodă 1 (buton):** Apasă **▶ Play**.
- **Metodă 2 (tastă rapidă):** Apasă **F9**.

Indicatorul devine **Playing** (pe fundal verde). Aplicația va executa pașii în ordine, evidențiind fiecare pas activ în lista de pași.

### Pasul 3 — Oprirea redării

- **Metodă 1 (buton):** Apasă **■ Stop**.
- **Metodă 2 (tastă rapidă):** Apasă **F10** (oprire de urgență).

---

## 8. Editarea pașilor înregistrați

Lista de pași din centrul ferestrei permite modificarea fiecărei acțiuni înregistrate.

### Toolbar-ul editorului

| Buton | Acțiune |
|-------|---------|
| **▲ Up** | Mută pasul selectat mai sus |
| **▼ Down** | Mută pasul selectat mai jos |
| **✎ Edit** | Deschide fereastra de editare a pasului |
| **⊕ Insert Wait** | Inserează o pauză (în secunde) după pasul selectat |
| **⊕ Duplicate** | Duplică pasul selectat |
| **✕ Delete** | Șterge pasul selectat |
| **☑ Toggle** | Activează / dezactivează pasul (pașii dezactivați sunt săriți la redare) |

### Editarea unui pas

1. **Selectează** un pas din listă cu un singur clic.
2. Apasă **✎ Edit** sau fă **dublu-clic** pe el.
3. Se deschide un dialog cu câmpurile editabile:
   - **Delay after (s)** — întârzierea după executarea pasului
   - **X / Y** — coordonatele de ecran (dacă este cazul)
   - **Key / Value** — tasta sau valoarea asociată
   - **Enabled** — bifă pentru a activa/dezactiva pasul
4. Apasă **Save** pentru a salva modificările.

---

## 9. Selectarea unei regiuni de ecran

Această funcție permite alegerea cu precizie a unei zone sau a unui punct de pe ecran.

### Cum se deschide selectorul de regiune

Apasă butonul **⊞ Select Region** din interfață.

Fereastra principală se minimizează, iar pe tot ecranul apare un **overlay semi-transparent** cu:
- 🎯 **Cursorul de tip crosshair** — un cursor în cruce pentru precizie maximă
- 📐 **Coordonate live** — coordonatele cursorului afișate în timp real
- 🔍 **Lupă (zoom ×4)** — o previzualizare mărită a zonei din jurul cursorului

### Cum se selectează o regiune

1. **Clic și trage** cu butonul stâng al mouse-ului pentru a desena un dreptunghi.
2. Pe măsură ce tragi, sunt afișate **dimensiunile selecției** (lățime × înălțime).
3. **Eliberează butonul mouse-ului** sau apasă **Enter** pentru a confirma selecția.
4. Apasă **Escape** pentru a anula și a reveni la fereastra principală.

Regiunea selectată apare în bara de stare: ex. `Region: (120, 80) 400×300`.

---

## 10. Salvarea și încărcarea unui macro

### Salvarea

1. Apasă butonul **💾 Save** sau folosește **Ctrl+S**.
2. Dacă macroul nu a fost salvat anterior, se deschide un dialog de tip **„Salvează ca"** — alege un folder și un nume de fișier (extensia `.json` se adaugă automat).
3. Fișierul este salvat în format JSON lizibil, conținând toate setările și pașii.

### Încărcarea

1. Apasă butonul **📂 Load** sau folosește **Ctrl+O**.
2. Navighează la fișierul `.json` pe care vrei să-l deschizi și apasă **Open**.
3. Toate setările și pașii din fișier se încarcă automat în aplicație.

### Crearea unui macro nou

- Apasă **Ctrl+N** sau accesează meniul **File → New**.
- Dacă există un macro nesalvat, ți se va cere confirmare.

---

## 11. Taste rapide globale

Aceste taste funcționează **oriunde pe calculator**, chiar dacă aplicația nu este în prim-plan:

| Tastă | Acțiune |
|-------|---------|
| **F8** | Pornire / Oprire înregistrare (comutare) |
| **F9** | Pornire redare macro |
| **F10** | Oprire de urgență (oprire imediată a orice) |

Taste rapide **în interiorul aplicației**:

| Combinație | Acțiune |
|------------|---------|
| **Ctrl+S** | Salvare macro |
| **Ctrl+O** | Deschidere macro |
| **Ctrl+N** | Macro nou |

---

## 12. Oprirea de urgență

Dacă macroul rulează și vrei să-l oprești imediat:

1. Apasă **F10** de oriunde de pe calculator — oprire imediată.
2. Sau apasă butonul **■ Stop** din interfață.

> **Important:** Oprirea de urgență este sigură — nu lasă taste sau butoane de mouse „blocate".

---

## 13. Probleme frecvente

### ❌ Eroarea: `ModuleNotFoundError: No module named 'pynput'`

**Cauză:** Dependințele nu sunt instalate.  
**Soluție:**
```bash
pip install -r requirements.txt
```

---

### ❌ Eroarea: `python: command not found`

**Cauză:** Python nu este instalat sau nu este în variabila PATH.  
**Soluție:** Descarcă și instalează Python de pe [python.org](https://www.python.org/downloads/).  
La instalare, bifează opțiunea **„Add Python to PATH"**.

---

### ❌ Pe Linux: tastele globale nu funcționează

**Cauză:** `pynput` necesită biblioteca `python3-xlib`.  
**Soluție:**
```bash
sudo apt install python3-xlib
```

---

### ❌ Pe macOS: înregistrarea nu captează input-ul

**Cauză:** macOS blochează accesul la input fără permisiune explicită.  
**Soluție:** Du-te la *Preferințe de sistem → Confidențialitate și securitate → Accesibilitate* și adaugă **Terminal** (sau **Python**) în lista de aplicații permise.

---

### ❌ Selectorul de regiune nu apare sau ecranul rămâne negru

**Cauză:** Pe unele sisteme, overlay-ul semi-transparent nu funcționează corect.  
**Soluție:** Încearcă să minimizezi manual fereastra aplicației înainte de a apăsa „Select Region".

---

### ❌ Macroul rulează prea repede / prea lent

**Soluție:** Ajustează câmpul **Speed** din bara de setări:
- Valoare mai mică (ex. `0.5`) → mai lent
- Valoare mai mare (ex. `2.0`) → mai rapid

---

## 14. Deschiderea paginii web (index.html)

Proiectul conține și un fișier `index.html` — o pagină web personală. Nu necesită Python sau nicio instalare; se deschide direct în orice browser.

### Pasul 1 — Localizează fișierul

Navighează în folderul proiectului. Vei găsi fișierul `index.html` în rădăcina acestuia.

### Pasul 2 — Deschide în browser

**Metodă 1 — Dublu-clic:**
Fă **dublu-clic** pe `index.html` din Explorer (Windows) sau Finder (macOS). Browserul implicit va deschide pagina automat.

**Metodă 2 — Drag & Drop:**
Trage fișierul `index.html` cu mouse-ul direct în fereastra browserului (Chrome, Firefox, Edge etc.).

**Metodă 3 — Din terminal:**
```bash
# Windows
start index.html

# macOS
open index.html

# Linux
xdg-open index.html
```

### Ce vei vedea

Pagina afișează un profil personal cu:
- O imagine de prezentare
- Un video despre Liceul „Dante Alighieri" din Chișinău
- O biografie personală

> **Notă:** Pagina încarcă imagini și video de pe internet (YouTube, site-uri externe). Asigură-te că ai o conexiune activă la internet pentru a le vedea corect.

---

*Ghid creat pentru aplicația Macro Recorder — Python Desktop Automation Tool.*
