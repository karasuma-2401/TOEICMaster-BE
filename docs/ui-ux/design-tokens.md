# Design Tokens & Foundations System

> Extracted from [specs/figma-screens-master.md](../specs/figma-screens-master.md) (Frame ID: )

# 1. DESIGN SYSTEM & DESIGN TOKENS OVERVIEW

The design on Figma has been upgraded to a complete version with its own dedicated page: **`Design System & Foundations` (Node ID: `11:2`)**, standardizing all Design Tokens and foundational Components.

```
                    ┌─────────────────────────────────────────────────────────┐
                    │               TOEICMASTER DESIGN SYSTEM                 │
                    ├────────────────────┬────────────────────────────────────┤
                    │ BRAND & SEMANTIC   │ EXAM CATEGORY THEMING (3 EXAMS)    │
                    │ Primary: #FF5A1F   │ L&R: Blue (#1D4ED8 / #EFF6FF)      │
                    │ Gradient: Red-Orng │ Speaking: Purple (#7E22CE / #FAF5FF│
                    │ Dark: #302C2A      │ Writing: Green (#047857 / #ECFDF5) │
                    └────────────────────┴────────────────────────────────────┘
```

### 1.1. Design Philosophy & Brand Color Palette (01 · Color tokens - Frame ID: `11:82`)

The color token system is organized into 4 distinct groups:

| Token Group | Figma Token Name | HEX Value | Role & Application |
| :--- | :--- | :--- | :--- |
| **Brand Primary** | `Primary` | `#FF5A1F` | Primary brand color, main CTA, focus border |
| | `Primary_Hover` | `#E04810` | Hover state of primary button |
| | `Primary_Light` | `#FFF7ED` | Active light background, accent badge, selection highlight |
| | `Primary_Dark` | `#C70E1A` | Bold accent brand text color, special state |
| | `Gradient_Start` | `#EC251B` | Start point of brand red gradient |
| | `Gradient_End` | `#FF6C0D` | End point of brand orange gradient |
| **Neutral Surface**| `Dark_Title` | `#302C2A` | Main headings, high-weight text |
| | `Dark_Body` | `#4C4542` | Standard content text (Body text) |
| | `Muted_Text` | `#77716E` | Secondary text, captions, timestamps, breadcrumbs |
| | `Placeholder` | `#AAA39F` | Hint text inside input fields (placeholder text) |
| | `Border_Light` | `#EEE9E6` | Subtle border separating lists, dividers |
| | `Border_Medium`| `#D9D2CE` | Input field borders, card borders, table borders |
| | `Bg_Warm` | `#FAF7F5` | Full-page background, practice area background |
| | `Bg_Surface` | `#FFFFFF` | Content Card background, Modal, Panel |
| | `Sidebar_Bg` | `#FAF8F7` | Fixed background of the Admin Sidebar |
| **Semantic State** | `Success_Text` | `#398565` | Active status text, correct answers |
| | `Success_Bg` | `#E5F4EC` | Success badge background, correct answer background |
| | `Success_Border`| `#A3D9C0` | Success notification border |
| | `Warning_Text` | `#D97706` | Warnings, draft status, incomplete questions |
| | `Warning_Bg` | `#FEF3C7` | Warning badge background |
| | `Danger_Text` | `#DC2626` | Validation errors, locked accounts, wrong answers |
| | `Danger_Bg` | `#FEE2E2` | Failed / Locked / Needs fixing badge background |
| | `Info_Text` | `#2563EB` | Instructional info, L&R skill label |
| | `Info_Bg` | `#EFF6FF` | Info badge background / Reminder banner |

---

### 1.2. Identifying Colors for 3 Independent Exams (Category Theming)

Under the standardized **v2.1 Independent Exam Foundations (`24:2`)** architecture, the TOEICMaster system distinguishes 3 independent exams through a visual color badge set:

```
┌─────────────────────────────────┬─────────────────────────────────┬─────────────────────────────────┐
│     LISTENING & READING (L&R)   │             SPEAKING            │             WRITING             │
├─────────────────────────────────┼─────────────────────────────────┼─────────────────────────────────┤
│ Text: #1D4ED8 (Dark Blue)       │ Text: #7E22CE (Deep Purple)     │ Text: #047857 (Emerald Green)   │
│ Background: #EFF6FF (Light Blue)│ Background: #FAF5FF (Light Prpl)│ Background: #ECFDF5 (Light Grn) │
│ 200 Qs · 120 mins · 990 Scale   │ 11 Qs · ~20 mins · 0-200 Scale  │ 8 Qs · 60 mins · 0-200 Scale    │
└─────────────────────────────────┴─────────────────────────────────┴─────────────────────────────────┘
```

---

### 1.3. Question Matrix States (Question Palette Exam Tokens)

The design standardizes colors for the Question Palette (applied in the L&R exam room and Review Mode screen):

| Palette Token Code | HEX Value | Meaning in Live Exam Room | Meaning in Review Mode |
| :--- | :--- | :--- | :--- |
| **Answered** | `#10B981` (Green) | Question where the learner has selected an answer | - |
| **Unanswered** | `#FFFFFF` (White) | Empty question, no answer yet | - |
| **Active** | `#FFF7ED` (Bg) / `#FF5A1F` (Border) / `#EA580C` (Text) | Question slot currently selected for answering / editing | Question slot currently being viewed in detail |
| **Flagged** | `#EAB308` (Yellow flag ⚑ icon) | Marked by the learner to check again | - |
| **Locked_Listening**| Bg `#F3F4F6`, Text `#9CA3AF` | Listening section question locked after audio passes | - |
| **Review_Correct** | `#10B981` (Green) | - | Correct answer |
| **Review_Incorrect**| `#EF4444` (Bright Red) | - | Incorrect answer |
| **Review_Skipped** | `#94A3B8` (Slate Gray) | - | Skipped question (unanswered) |

---

### 1.4. Typography System (02 · Typography - Frame ID: `11:184`)

Consistently uses the **`Poppins`** font family with clear line-height ratios and hierarchy standards:

- **Display Hero / Score 990**: `48px Bold` (Used for L&R mock exam total score, prominent scores).
- **Display 1**: `32px Bold` (Used for home page headline, large exam room titles).
- **Heading 1 / Score 200**: `24px SemiBold / Bold` (Page titles, Speaking & Writing 200-scale scores).
- **Heading 2**: `18px SemiBold` (Part titles, large Card titles, exam names).
- **Heading 3**: `15px SemiBold` (Feature group titles, Speaking phase statuses).
- **Body Large**: `15px Regular` (Short hero descriptions, general exam instructions).
- **Body Base**: `13px Regular` (Question content, multiple-choice options, reading passages, data tables).
- **Body Small**: `11px Regular / SemiBold` (Matrix slot numbers, tag badges, breadcrumbs, footnotes).
- **Code Timer**: `20px Bold` (Monospace exam countdown timer `01:45:12`).

---

### 1.5. Master Components & States Library (03 · Frame ID: `11:194`)

Specification for shared UI components and state variants:
1. **Button Master**: Primary Gradient, Primary Solid, Secondary Outlined, Ghost, Disabled.
2. **Input Fields**: Normal, Hover, Focus (orange border `#FF5A1F`), Error (red border `#DC2626` with error message *"Invalid email"* below), Disabled.
3. **Timer Module (Exam Room Clock)**:
   - Normal (Time > 5 mins): Dark gray text `01:45:12`.
   - Warning (Time `<= 5 mins`): Light amber background, orange text `#D97706` `00:04:59`.
   - Urgent (Time `<= 1 min`): Light red background, blinking red text `#DC2626` `00:00:59`.
4. **Speaking Two-Phase Recorder Component**:
   - `PHASE 1 · MIC NOT RECORDING`: 45s preparation, countdown `00:32`, static gray mic waveform, text: *"Begin speaking after the Beep"*.
   - `PHASE 2 · ● REC`: 30s response, countdown `00:24`, blinking red REC dot, audio waveform oscillating with the learner's actual volume.
5. **Writing Anti-Cheat Workspace Component**:
   - Typing area with right-click disabled and paste shortcuts `Ctrl+V` / `Cmd+V` disabled.
   - Real-time Word Counter displayed as: *"320 words / Recommended 300 words"*.

---

### 1.6. 3 Independent Exam Foundations (04 · Frame ID: `24:2`)

The TOEICMaster system defines 3 independent exams:
1. **Listening & Reading (L&R) Exam**:
   - Structure: 200 multiple-choice questions (Part 1 - Part 7).
   - Duration: 120 continuous minutes (45 minutes Listening + 75 minutes Reading).
   - Mechanism: Master Audio plays continuously throughout the Listening section, 200-question Question Palette.
   - Score scale: 10-990 ETS points.
2. **Speaking Exam**:
   - Structure: 11 questions (Part 1 - Part 5).
   - Duration: ~20 minutes.
   - Mechanism: Pre-exam Microphone check, automated 2-phase workflow (Phase 1 Preparation -> Beep -> Phase 2 Response Recording).
   - Score scale: 0-200 ETS points.
3. **Writing Exam**:
   - Structure: 8 questions (Part 1 Sentence writing: 5 questions; Part 2 Email response: 2 questions; Part 3 Essay writing: 1 prompt).
   - Duration: 60 minutes.
   - Mechanism: Anti-cheat text editor, paste blocking, automatic word counting.
   - Score scale: 0-200 ETS points.

---
