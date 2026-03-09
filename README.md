# 🖱️⌨️ Macro Recorder — Ghid de instalare / Installation Guide

> **Limbă / Language:** Română 🇷🇴 · English 🇬🇧

---

## Ce face această aplicație? / What does this app do?

Aplicația **înregistrează** toate mișcările de mouse și apăsările de taste pe care le faci, și le **repetă** automat, exact cum le-ai făcut tu.

This app **records** all your mouse movements and key presses, then **replays** them automatically, exactly as you performed them.

---

## Pasul 1 — Instalează Python / Step 1 — Install Python

> Dacă ai deja Python 3 instalat, sari la Pasul 2.  
> If you already have Python 3 installed, skip to Step 2.

### 🪟 Windows

1. Deschide browserul și accesează: **https://www.python.org/downloads/**  
   *(Open your browser and go to https://www.python.org/downloads/)*
2. Apasă butonul galben **"Download Python 3.x.x"**  
   *(Click the yellow **"Download Python 3.x.x"** button)*
3. Rulează fișierul `.exe` descărcat.  
   *(Run the downloaded `.exe` file)*
4. ⚠️ **IMPORTANT:** Bifează **"Add Python to PATH"** înainte de a apăsa Install!  
   *(Tick **"Add Python to PATH"** before clicking Install!)*
5. Apasă **"Install Now"** și așteaptă.  
   *(Click **"Install Now"** and wait)*

Verificare / Verify — deschide **Command Prompt** (`Win + R` → tastează `cmd` → Enter) și scrie:
```
python --version
```
Trebuie să apară ceva de genul `Python 3.12.0`.  
*(You should see something like `Python 3.12.0`)*

---

### 🍎 macOS

1. Deschide **Terminal** (`Cmd + Space` → scrie `Terminal` → Enter).  
   *(Open **Terminal**)*
2. Instalează Python cu **Homebrew** (recomandat):  
   *(Install Python with **Homebrew** — recommended)*
   ```bash
   /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
   brew install python
   ```
   Sau descarcă direct de pe **https://www.python.org/downloads/** și urmează pașii.  
   *(Or download directly from https://www.python.org/downloads/ and follow the steps)*

---

### 🐧 Linux (Ubuntu / Debian)

Deschide **Terminal** și rulează:  
*(Open **Terminal** and run)*
```bash
sudo apt update
sudo apt install python3 python3-pip -y
```

---

## Pasul 2 — Descarcă fișierele aplicației / Step 2 — Download the app files

### Varianta A — cu Git (recomandat) / Option A — with Git (recommended)

```bash
git clone https://github.com/Mymady10/Marius7654635.git
cd Marius7654635
```

### Varianta B — fără Git / Option B — without Git

1. Accesează **https://github.com/Mymady10/Marius7654635**  
   *(Go to https://github.com/Mymady10/Marius7654635)*
2. Apasă butonul verde **`<> Code`** → **`Download ZIP`**  
   *(Click the green **`<> Code`** button → **`Download ZIP`**)*
3. Dezarhivează fișierul ZIP undeva pe calculator (ex: Desktop).  
   *(Extract the ZIP somewhere on your computer, e.g. Desktop)*
4. Deschide folderul dezarhivat.  
   *(Open the extracted folder)*

---

## Pasul 3 — Instalează dependențele / Step 3 — Install dependencies

Deschide **Command Prompt** / **Terminal** în folderul aplicației și rulează:  
*(Open **Command Prompt** / **Terminal** inside the app folder and run)*

### 🪟 Windows
```
pip install -r requirements.txt
```

### 🍎 macOS / 🐧 Linux
```bash
pip3 install -r requirements.txt
```

> **Cum deschid Command Prompt în folderul potrivit? / How to open CMD in the right folder?**  
> 🪟 Windows: în File Explorer, navighează în folder, apasă bara de adresă, scrie `cmd` și apasă Enter.  
> *(In File Explorer, navigate to the folder, click the address bar, type `cmd` and press Enter)*

---

## Pasul 4 — Pornește aplicația / Step 4 — Run the app

### 🪟 Windows
```
python macro_recorder.py
```

### 🍎 macOS / 🐧 Linux
```bash
python3 macro_recorder.py
```

Vei vedea un ecran ca acesta / You will see a screen like this:
```
╔══════════════════════════════════════════════════╗
║         MACRO RECORDER  –  Dimitriu Marius       ║
╚══════════════════════════════════════════════════╝

Taste rapide / Hotkeys:
  F8    →  Pornește/Oprește înregistrarea  (Start/Stop recording)
  F9    →  Redă înregistrarea              (Replay recording)
  F10   →  Salvează înregistrarea           (Save recording to JSON)
  F11   →  Încarcă înregistrarea            (Load recording from JSON)
  F12   →  Ieșire                           (Quit)
```

---

## Cum folosesc aplicația? / How do I use the app?

| Tastă / Key | Ce face / What it does |
|-------------|------------------------|
| **F8** | Pornește înregistrarea. Apasă din nou pentru a opri. *(Start recording. Press again to stop.)* |
| **F9** | Repetă tot ce ai înregistrat. *(Replays everything you recorded.)* |
| **F10** | Salvează înregistrarea în fișierul `macro_recording.json` pe disc. *(Saves the recording to disk.)* |
| **F11** | Încarcă o înregistrare salvată anterior. *(Loads a previously saved recording.)* |
| **F12** | Închide aplicația. *(Closes the app.)* |

### Exemplu de utilizare / Usage example

1. Apasă **F8** — aplicația începe să înregistreze.  
   *(Press **F8** — the app starts recording)*
2. Fă orice acțiuni vrei pe calculator (click-uri, scriere, scroll).  
   *(Perform any actions you want on the computer)*
3. Apasă **F8** din nou — înregistrarea se oprește.  
   *(Press **F8** again — recording stops)*
4. Apasă **F9** — aplicația repetă exact aceleași acțiuni.  
   *(Press **F9** — the app replays the exact same actions)*

---

## Probleme frecvente / Troubleshooting

| Problemă / Problem | Soluție / Solution |
|--------------------|--------------------|
| `'python' is not recognized` | Reinstalează Python și bifează **"Add Python to PATH"** |
| `ModuleNotFoundError: pynput` | Rulează din nou `pip install -r requirements.txt` |
| Mouse/tastatura nu se mișcă la redare (Linux) | Rulează `sudo python3 macro_recorder.py` |
| Mouse/tastatura nu se mișcă la redare (macOS) | Du-te la **System Settings → Privacy & Security → Accessibility** și adaugă **Terminal** / **Python** |

---

## Cerințe de sistem / System requirements

- **Python** 3.8 sau mai nou / or newer
- **Sistem de operare / OS:** Windows 10+, macOS 10.13+, Linux (X11)
- **Bibliotecă / Library:** `pynput` (instalată automat la Pasul 3 / installed automatically at Step 3)
