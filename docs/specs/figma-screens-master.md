# FIGMA DESIGN SCREENS SPECIFICATION & DETAILED DESCRIPTION
## 4-SKILL ONLINE TOEIC PREPARATION SYSTEM (TOEICMASTER)

---

| **Document Information** | **Details** |
| :--- | :--- |
| **Project** | **TOEICMaster** - Online 4-Skill TOEIC Preparation and Proficiency Assessment Platform |
| **Author Role** | **Senior UX/UI Designer & Product Design Lead** |
| **Figma Design Source** | [Figma File: TOEICMaster (node-id: 0-1)](https://www.figma.com/design/maKDgFmFHPvMoqeHBMW4US/TOEICMaster?node-id=0-1&p=f&t=O8G3nhSqmbtUH4KL-0) |
| **Design Version** | **2.1 (Full Ecosystem: 44 Screens + Design System Foundations)** |
| **Update Status** | November 2026 (Comprehensive sync with the latest Figma designs) |
| **Target Audience** | Product Owner, Frontend Developer, Backend Developer, QA/QC Engineer |
| **Document Purpose** | Detailed specification of every screen (layout, components, copy, interactions, URL routes, edge cases) for UI design and frontend/backend development |

---

## TABLE OF CONTENTS

1. [DESIGN SYSTEM & DESIGN TOKENS OVERVIEW (PAGE: DESIGN SYSTEM & FOUNDATIONS)](#1-design-system--design-tokens-overview)
   - 1.1. Design Philosophy & Brand Color Palette (01 · Color Tokens - ID: `11:82`)
   - 1.2. Independent Exam Category Theming (L&R, Speaking, Writing)
   - 1.3. Question Palette Exam Tokens
   - 1.4. Typography System (02 · Typography - ID: `11:184`)
   - 1.5. Master Components & States Library (03 · Master Components - ID: `11:194`)
   - 1.6. 3 Independent Exam Foundations (04 · v2.1 Independent Exam Foundations - ID: `24:2`)
2. [SUMMARY TABLE OF ALL 44 EXISTING FIGMA SCREENS (PAGE 1 - ID: `0:1`)](#2-summary-table-of-all-44-existing-figma-screens)
3. [DETAILED SCREEN SPECIFICATIONS BY SUBSYSTEM](#3-detailed-screen-specifications-by-subsystem)
   - **SUBSYSTEM 1: LEARNER AUTHENTICATION (LEARNER AUTH - 3 SCREENS)**
     - [Screen 01: [Learner] Login_Page - /login (ID: 1:2)](#screen-01-learner-login_page---login-frame-id-12)
     - [Screen 02: [Learner] Register_Page - /register (ID: 1:144)](#screen-02-learner-register_page---register-frame-id-1144)
     - [Screen 03: [Learner] Forgot_Password_Page - /forgot-password (ID: 1:294)](#screen-03-learner-forgot_password_page---forgot-password-frame-id-1294)
   - **SUBSYSTEM 2: LEARNER PORTAL, PROFILE & PROGRESSION HISTORY (LEARNER PORTAL & PROGRESSION - 3 SCREENS)**
     - [Screen 04: [Learner] Home_Landing_Page - / (ID: 13:67084)](#screen-04-learner-home_landing_page----frame-id-1367084)
     - [Screen 05: [Learner] Profile_Settings_Page - /profile (ID: 13:70116)](#screen-05-learner-profile_settings_page---profile-frame-id-1370116)
     - [Screen 06: [Learner] History_Progression_Page - /history (ID: 13:70205)](#screen-06-learner-history_progression_page---history-frame-id-1370205)
   - **SUBSYSTEM 3: LEARNER PRACTICE HUB (4 SCREENS)**
     - [Screen 07: [Learner] Practice_Hub_Overview - /practice (ID: 13:67225)](#screen-07-learner-practice_hub_overview---practice-frame-id-1367225)
     - [Screen 08: [Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId] (ID: 13:67472)](#screen-08-learner-practice_lr_interactive_mode---practicelrpartid-frame-id-1367472)
     - [Screen 09: [Learner] Practice_Speaking_Mode - /practice/speaking/[partId] (ID: 13:67617)](#screen-09-learner-practice_speaking_mode---practicespeakingpartid-frame-id-1367617)
     - [Screen 10: [Learner] Practice_Writing_Mode - /practice/writing/[partId] (ID: 13:67739)](#screen-10-learner-practice_writing_mode---practicewritingpartid-frame-id-1367739)
   - **SUBSYSTEM 4: ETS MOCK EXAMS LIBRARY HUB (LEARNER EXAMS LIBRARY HUB - 4 SCREENS)**
     - [Screen 11: [Learner] Exams_Library_Hub - /exams (ID: 13:67810)](#screen-11-learner-exams_library_hub---exams-frame-id-1367810)
     - [Screen 12: [Learner] Exams_LR_List - /exams/lr (ID: 24:8730)](#screen-12-learner-exams_lr_list---examslr-frame-id-248730)
     - [Screen 13: [Learner] Exams_Speaking_List - /exams/speaking (ID: 24:8867)](#screen-13-learner-exams_speaking_list---examsspeaking-frame-id-248867)
     - [Screen 14: [Learner] Exams_Writing_List - /exams/writing (ID: 24:9318)](#screen-14-learner-exams_writing_list---examswriting-frame-id-249318)
   - **SUBSYSTEM 5: LISTENING & READING EXAM & EVALUATION FLOW (LEARNER L&R EXAM FLOW - 5 SCREENS)**
     - [Screen 15: [Learner] Exam_LR_Instructions - /exams/lr/[id] (ID: 13:67968)](#screen-15-learner-exam_lr_instructions---examslrid-frame-id-1367968)
     - [Screen 16: [Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt (ID: 13:68036)](#screen-16-learner-exam_lr_virtual_room---examslridattempt-frame-id-1368036)
     - [Screen 17: [Learner] Modal_Exam_LR_Confirm_Submit - Overlay (ID: 13:68592)](#screen-17-learner-modal_exam_lr_confirm_submit---overlay-frame-id-1368592)
     - [Screen 18: [Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId] (ID: 13:69428)](#screen-18-learner-exam_lr_result_report---examslridresultattemptid-frame-id-1369428)
     - [Screen 19: [Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId] (ID: 13:69536)](#screen-19-learner-exam_lr_review_mode---examslridreviewattemptid-frame-id-1369536)
   - **SUBSYSTEM 6: SPEAKING EXAM & EVALUATION FLOW (LEARNER SPEAKING EXAM FLOW - 4 SCREENS)**
     - [Screen 20: [Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check (ID: 13:69176)](#screen-20-learner-exam_speaking_device_check---examsspeakingiddevice-check-frame-id-1369176)
     - [Screen 21: [Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt (ID: 13:69278)](#screen-21-learner-exam_speaking_virtual_room---examsspeakingidattempt-frame-id-1369278)
     - [Screen 22: [Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId] (ID: 24:9004)](#screen-22-learner-exam_speaking_result_report---examsspeakingidresultattemptid-frame-id-249004)
     - [Screen 23: [Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId] (ID: 24:9106)](#screen-23-learner-exam_speaking_review_mode---examsspeakingidreviewattemptid-frame-id-249106)
   - **SUBSYSTEM 7: WRITING EXAM & EVALUATION FLOW (LEARNER WRITING EXAM FLOW - 4 SCREENS)**
     - [Screen 24: [Learner] Exam_Writing_Instructions - /exams/writing/[id] (ID: 24:9455)](#screen-24-learner-exam_writing_instructions---examswritingid-frame-id-249455)
     - [Screen 25: [Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt (ID: 13:69370)](#screen-25-learner-exam_writing_virtual_room---examswritingidattempt-frame-id-1369370)
     - [Screen 26: [Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId] (ID: 24:9528)](#screen-26-learner-exam_writing_result_report---examswritingidresultattemptid-frame-id-249528)
     - [Screen 27: [Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId] (ID: 24:9605)](#screen-27-learner-exam_writing_review_mode---examswritingidreviewattemptid-frame-id-249605)
   - **SUBSYSTEM 8: ADMIN MANAGEMENT - OVERVIEW & USERS (ADMIN CORE - 4 SCREENS)**
     - [Screen 28: [Admin] Overview_Dashboard - /admin/overview (ID: 1:823)](#screen-28-admin-overview_dashboard---adminoverview-frame-id-1823)
     - [Screen 29: [Admin] Users_Management - /admin/users (ID: 1:393)](#screen-29-admin-users_management---adminusers-frame-id-1393)
     - [Screen 30: [Admin] Users_Loading_State - /admin/users?state=loading (ID: 13:70854)](#screen-30-admin-users_loading_state---adminusersstateloading-frame-id-1370854)
     - [Screen 31: [Admin] Users_Empty_State - /admin/users?state=empty (ID: 13:70775)](#screen-31-admin-users_empty_state---adminusersstateempty-frame-id-1370775)
   - **SUBSYSTEM 9: ADMIN MANAGEMENT - 4-SKILL EXERCISE BANK (EXERCISE BANK CMS - 8 SCREENS)**
     - [Screen 32: [Admin] Exercises_LR_Parts - /admin/exercises/lr (ID: 1:1116)](#screen-32-admin-exercises_lr_parts---adminexerciseslr-frame-id-11116)
     - [Screen 33: [Admin] Listening_Part1_Management - /admin/exercises/lr/p1 (ID: 1:1389)](#screen-33-admin-listening_part1_management---adminexerciseslrp1-frame-id-11389)
     - [Screen 34: [Admin] Reading_Part7_Management - /admin/exercises/lr/p7 (ID: 13:70307)](#screen-34-admin-reading_part7_management---adminexerciseslrp7-frame-id-1370307)
     - [Screen 35: [Admin] Modal_Add_Reading_Part7_Cluster - Overlay (ID: 13:70476)](#screen-35-admin-modal_add_reading_part7_cluster---overlay-frame-id-1370476)
     - [Screen 36: [Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1 (ID: 1:1804)](#screen-36-admin-speaking_part1_management---adminexercisesspeakingp1-frame-id-11804)
     - [Screen 37: [Admin] Modal_Import_Exercises_Excel - Overlay (ID: 1:2218)](#screen-37-admin-modal_import_exercises_excel---overlay-frame-id-12218)
     - [Screen 38: [Admin] Writing_Parts - /admin/exercises/writing (ID: 1:2674)](#screen-38-admin-writing_parts---adminexerciseswriting-frame-id-12674)
     - [Screen 39: [Admin] Writing_Part1_Management - /admin/exercises/writing/p1 (ID: 1:2875)](#screen-39-admin-writing_part1_management---adminexerciseswritingp1-frame-id-12875)
   - **SUBSYSTEM 10: ADMIN MANAGEMENT - EXAMS & EXAM BUILDER (EXAM BUILDER CMS - 5 SCREENS)**
     - [Screen 40: [Admin] Exams_Management - /admin/exams/lr (ID: 1:3237)](#screen-40-admin-exams_management---adminexamslr-frame-id-13237)
     - [Screen 41: [Admin] Exams_Speaking_Management - /admin/exams/speaking (ID: 24:9700)](#screen-41-admin-exams_speaking_management---adminexamsspeaking-frame-id-249700)
     - [Screen 42: [Admin] Exams_Writing_Management - /admin/exams/writing (ID: 24:9863)](#screen-42-admin-exams_writing_management---adminexamswriting-frame-id-249863)
     - [Screen 43: [Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit (ID: 1:3475)](#screen-43-admin-exambuilder_lr_200slots---adminexamslridedit-frame-id-13475)
     - [Screen 44: [Admin] Modal_Create_Exam_Metadata - Overlay (ID: 1:4227)](#screen-44-admin-modal_create_exam_metadata---overlay-frame-id-14227)
4. [UX/UI PROFESSIONAL ASSESSMENT & DESIGN EVOLUTION COMPARISON (DESIGN AUDIT V1.0 -> V2.1)](#4-uxui-professional-assessment--design-evolution-comparison)
5. [FRONTEND IMPLEMENTATION & CONTRACT INTEGRATION GUIDE](#5-frontend-implementation--contract-integration-guide)

---

# 1. DESIGN SYSTEM & DESIGN TOKENS OVERVIEW

The Figma design has been upgraded to a complete version with a dedicated page: **`Design System & Foundations` (Node ID: `11:2`)**, standardizing all foundational Design Tokens and Components.

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

| Token Group | Figma Token Name | HEX Value | Role & Applied Locations |
| :--- | :--- | :--- | :--- |
| **Brand Primary** | `Primary` | `#FF5A1F` | Primary brand color, main CTAs, focus borders |
| | `Primary_Hover` | `#E04810` | Hover state for primary buttons |
| | `Primary_Light` | `#FFF7ED` | Active light background, emphasis badges, selection highlights |
| | `Primary_Dark` | `#C70E1A` | Strong emphasis brand text, special states |
| | `Gradient_Start` | `#EC251B` | Start color of the brand red-orange gradient |
| | `Gradient_End` | `#FF6C0D` | End color of the brand red-orange gradient |
| **Neutral Surface**| `Dark_Title` | `#302C2A` | Primary headings, high-weight text |
| | `Dark_Body` | `#4C4542` | Standard body text |
| | `Muted_Text` | `#77716E` | Secondary text, captions, timestamps, breadcrumbs |
| | `Placeholder` | `#AAA39F` | Placeholder text inside input fields |
| | `Border_Light` | `#EEE9E6` | Subtle borders separating list items, dividers |
| | `Border_Medium`| `#D9D2CE` | Input field borders, card borders, table borders |
| | `Bg_Warm` | `#FAF7F5` | Full-page background, practice area background |
| | `Bg_Surface` | `#FFFFFF` | Content card background, modals, panels |
| | `Sidebar_Bg` | `#FAF8F7` | Fixed background of the Admin Sidebar |
| **Semantic State** | `Success_Text` | `#398565` | Active status text, correct answers |
| | `Success_Bg` | `#E5F4EC` | Success badge background, correct answer background |
| | `Success_Border`| `#A3D9C0` | Success notification border |
| | `Warning_Text` | `#D97706` | Warnings, draft status, incomplete questions |
| | `Warning_Bg` | `#FEF3C7` | Warning badge background |
| | `Danger_Text` | `#DC2626` | Validation errors, locked accounts, incorrect answers |
| | `Danger_Bg` | `#FEE2E2` | Failed / Locked / Needs Fixing badge background |
| | `Info_Text` | `#2563EB` | Instructional info, L&R skill labels |
| | `Info_Bg` | `#EFF6FF` | Info badge background / Reminder banners |

---

### 1.2. Independent Exam Category Theming

Following the standardized **v2.1 Independent Exam Foundations (`24:2`)** architecture, TOEICMaster distinguishes 3 independent exams through a visual category color set:

```
┌─────────────────────────────────┬─────────────────────────────────┬─────────────────────────────────┐
│     LISTENING & READING (L&R)   │             SPEAKING            │             WRITING             │
├─────────────────────────────────┼─────────────────────────────────┼─────────────────────────────────┤
│ Text: #1D4ED8 (Dark Blue)       │ Text: #7E22CE (Deep Purple)     │ Text: #047857 (Emerald Green)   │
│ Background: #EFF6FF (Light Blue)│ Background: #FAF5FF (Light Purp)│ Background: #ECFDF5 (Light Grn) │
│ 200 Qs · 120 mins · 990 Scale   │ 11 Qs · ~20 mins · 0-200 Scale  │ 8 Qs · 60 mins · 0-200 Scale    │
└─────────────────────────────────┴─────────────────────────────────┴─────────────────────────────────┘
```

---

### 1.3. Question Palette Exam Tokens

The design standardizes colors for the Question Palette (used in the L&R virtual exam room and Review Mode):

| Palette Token | HEX Value | Meaning in Live Exam Room | Meaning in Review Mode |
| :--- | :--- | :--- | :--- |
| **Answered** | `#10B981` (Green) | Question has an answer selected by the learner | - |
| **Unanswered** | `#FFFFFF` (White) | Question is blank, no answer selected yet | - |
| **Active** | `#FFF7ED` (Bg) / `#FF5A1F` (Border) / `#EA580C` (Text) | Question slot currently being answered / edited | Question slot currently viewed in detail |
| **Flagged** | `#EAB308` (Yellow flag ⚑ icon) | Marked by the learner for later review | - |
| **Locked_Listening**| Bg `#F3F4F6`, Text `#9CA3AF` | Listening question locked once audio has passed | - |
| **Review_Correct** | `#10B981` (Green) | - | Correct answer |
| **Review_Incorrect**| `#EF4444` (Bright Red) | - | Incorrect answer |
| **Review_Skipped** | `#94A3B8` (Slate Gray) | - | Skipped question (unanswered) |

---

### 1.4. Typography System (02 · Typography - Frame ID: `11:184`)

Uses the **`Poppins`** font family consistently with clearly defined line-height ratios and hierarchy:

- **Display Hero / Score 990**: `48px Bold` (Used for L&R mock exam total score, prominent score displays).
- **Display 1**: `32px Bold` (Used for homepage hero headline, large exam room titles).
- **Heading 1 / Score 200**: `24px SemiBold / Bold` (Page titles, Speaking & Writing 200-scale scores).
- **Heading 2**: `18px SemiBold` (Part titles, large card titles, exam names).
- **Heading 3**: `15px SemiBold` (Feature group titles, Speaking phase states).
- **Body Large**: `15px Regular` (Short hero descriptions, overall exam instructions).
- **Body Base**: `13px Regular` (Question text, multiple-choice options, reading passages, data tables).
- **Body Small**: `11px Regular / SemiBold` (Palette cell numbers, tag badges, breadcrumbs, footnotes).
- **Code Timer**: `20px Bold` (Monospace exam countdown timer `01:45:12`).

---

### 1.5. Master Components & States Library (03 · Frame ID: `11:194`)

Specification of shared UI components and variant states:
1. **Button Master**: Primary Gradient, Primary Solid, Secondary Outlined, Ghost, Disabled.
2. **Input Fields**: Normal, Hover, Focus (orange border `#FF5A1F`), Error (red border `#DC2626` with error message *"Invalid email"* underneath), Disabled.
3. **Timer Module (Exam Room Timer)**:
   - Normal (Time > 5 minutes): Dark gray text `01:45:12`.
   - Warning (Time `<= 5 minutes`): Light amber-orange background, orange text `#D97706` `00:04:59`.
   - Urgent (Time `<= 1 minute`): Light red background, blinking red text `#DC2626` `00:00:59`.
4. **Speaking Two-Phase Recorder Component**:
   - `PHASE 1 · MIC NOT RECORDING`: 45s preparation, countdown `00:32`, static gray mic waveform, text: *"Begin speaking after the Beep"*.
   - `PHASE 2 · ● REC`: 30s response, countdown `00:24`, blinking red REC dot, live audio waveform oscillating with the learner's voice volume.
5. **Writing Anti-Cheat Workspace Component**:
   - Text editor disables right-click and paste shortcuts `Ctrl+V` / `Cmd+V`.
   - Automatic real-time word counter displayed as: *"320 words / Recommended 300 words"*.

---

### 1.6. 3 Independent Exam Foundations (04 · Frame ID: `24:2`)

TOEICMaster defines 3 independent exams:
1. **Listening & Reading (L&R) Exam**:
   - Structure: 200 multiple-choice questions (Part 1 - Part 7).
   - Duration: 120 continuous minutes (45 minutes Listening + 75 minutes Reading).
   - Mechanism: Master Audio plays continuously throughout the Listening section, 200-question Question Palette.
   - Score scale: 10-990 ETS scale.
2. **Speaking Exam**:
   - Structure: 11 questions (Part 1 - Part 5).
   - Duration: ~20 minutes.
   - Mechanism: Pre-exam microphone device check, automated 2-phase workflow (Phase 1 Preparation → Beep → Phase 2 Response Recording).
   - Score scale: 0-200 ETS scale.
3. **Writing Exam**:
   - Structure: 8 questions (Part 1 Sentence writing: 5 questions; Part 2 Email response: 2 questions; Part 3 Essay writing: 1 essay).
   - Duration: 60 minutes.
   - Mechanism: Anti-cheat text editor, paste blocked, automatic word count.
   - Score scale: 0-200 ETS scale.

---

# 2. SUMMARY TABLE OF ALL 44 EXISTING FIGMA SCREENS

All 44 screens are arranged visually on **Page 1 (Node ID: `0:1`)**, named according to the `[Role] Name - /route` convention:

| No. | Figma Frame Name | Node ID | Dimensions (W x H) | Subsystem | Frontend Route Path |
| :---: | :--- | :---: | :---: | :---: | :--- |
| **01** | `[Learner] Login_Page - /login` | `1:2` | 1722 x 836 px | Auth | `/login` |
| **02** | `[Learner] Register_Page - /register` | `1:144` | 1722 x 836 px | Auth | `/register` |
| **03** | `[Learner] Forgot_Password_Page - /forgot-password` | `1:294` | 1722 x 836 px | Auth | `/forgot-password` |
| **04** | `[Learner] Home_Landing_Page - /` | `13:67084` | 1722 x 1275 px | Portal | `/` |
| **05** | `[Learner] Profile_Settings_Page - /profile` | `13:70116` | 1722 x 875 px | Portal | `/profile` |
| **06** | `[Learner] History_Progression_Page - /history` | `13:70205` | 1722 x 1985 px | Portal | `/history` |
| **07** | `[Learner] Practice_Hub_Overview - /practice` | `13:67225` | 1722 x 965 px | Practice | `/practice` |
| **08** | `[Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId]` | `13:67472` | 1722 x 1000 px | Practice | `/practice/lr/[partId]` |
| **09** | `[Learner] Practice_Speaking_Mode - /practice/speaking/[partId]` | `13:67617` | 1722 x 850 px | Practice | `/practice/speaking/[partId]` |
| **10** | `[Learner] Practice_Writing_Mode - /practice/writing/[partId]` | `13:67739` | 1722 x 982 px | Practice | `/practice/writing/[partId]` |
| **11** | `[Learner] Exams_Library_Hub - /exams` | `13:67810` | 1722 x 950 px | Exams | `/exams` |
| **12** | `[Learner] Exams_LR_List - /exams/lr` | `24:8730` | 1722 x 918 px | Exams | `/exams/lr` |
| **13** | `[Learner] Exams_Speaking_List - /exams/speaking` | `24:8867` | 1722 x 918 px | Exams | `/exams/speaking` |
| **14** | `[Learner] Exams_Writing_List - /exams/writing` | `24:9318` | 1722 x 918 px | Exams | `/exams/writing` |
| **15** | `[Learner] Exam_LR_Instructions - /exams/lr/[id]` | `13:67968` | 1722 x 895 px | Exam Flow L&R | `/exams/lr/[id]` |
| **16** | `[Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt` | `13:68036` | 1722 x 902 px | Exam Flow L&R | `/exams/lr/[id]/attempt` |
| **17** | `[Learner] Modal_Exam_LR_Confirm_Submit - Overlay` | `13:68592` | 1722 x 900 px | Exam Flow L&R | Overlay Modal |
| **18** | `[Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId]` | `13:69428` | 1722 x 950 px | Exam Flow L&R | `/exams/lr/[id]/result/[attemptId]` |
| **19** | `[Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId]` | `13:69536` | 1722 x 1050 px | Exam Flow L&R | `/exams/lr/[id]/review/[attemptId]` |
| **20** | `[Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check` | `13:69176` | 1722 x 843 px | Exam Flow Speaking | `/exams/speaking/[id]/device-check` |
| **21** | `[Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt` | `13:69278` | 1722 x 932 px | Exam Flow Speaking | `/exams/speaking/[id]/attempt` |
| **22** | `[Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId]` | `24:9004` | 1722 x 998 px | Exam Flow Speaking | `/exams/speaking/[id]/result/[attemptId]` |
| **23** | `[Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId]` | `24:9106` | 1722 x 1000 px | Exam Flow Speaking | `/exams/speaking/[id]/review/[attemptId]` |
| **24** | `[Learner] Exam_Writing_Instructions - /exams/writing/[id]` | `24:9455` | 1722 x 864 px | Exam Flow Writing | `/exams/writing/[id]` |
| **25** | `[Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt` | `13:69370` | 1722 x 882 px | Exam Flow Writing | `/exams/writing/[id]/attempt` |
| **26** | `[Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId]` | `24:9528` | 1722 x 900 px | Exam Flow Writing | `/exams/writing/[id]/result/[attemptId]` |
| **27** | `[Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId]` | `24:9605` | 1722 x 1478 px | Exam Flow Writing | `/exams/writing/[id]/review/[attemptId]` |
| **28** | `[Admin] Overview_Dashboard - /admin/overview` | `1:823` | 1722 x 1200 px | Admin Core | `/admin/overview` |
| **29** | `[Admin] Users_Management - /admin/users` | `1:393` | 1722 x 871 px | Admin Core | `/admin/users` |
| **30** | `[Admin] Users_Loading_State - /admin/users?state=loading` | `13:70854` | 1722 x 948 px | Admin Core | `/admin/users?state=loading` |
| **31** | `[Admin] Users_Empty_State - /admin/users?state=empty` | `13:70775` | 1722 x 908 px | Admin Core | `/admin/users?state=empty` |
| **32** | `[Admin] Exercises_LR_Parts - /admin/exercises/lr` | `1:1116` | 1722 x 915 px | Exercise CMS | `/admin/exercises/lr` |
| **33** | `[Admin] Listening_Part1_Management - /admin/exercises/lr/p1` | `1:1389` | 1722 x 836 px | Exercise CMS | `/admin/exercises/lr/p1` |
| **34** | `[Admin] Reading_Part7_Management - /admin/exercises/lr/p7` | `13:70307` | 1722 x 836 px | Exercise CMS | `/admin/exercises/lr/p7` |
| **35** | `[Admin] Modal_Add_Reading_Part7_Cluster - Overlay` | `13:70476` | 1722 x 1100 px | Exercise CMS | Overlay Modal |
| **36** | `[Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1` | `1:1804` | 1722 x 836 px | Exercise CMS | `/admin/exercises/speaking/p1` |
| **37** | `[Admin] Modal_Import_Exercises_Excel - Overlay` | `1:2218` | 1722 x 836 px | Exercise CMS | Overlay Modal |
| **38** | `[Admin] Writing_Parts - /admin/exercises/writing` | `1:2674` | 1722 x 836 px | Exercise CMS | `/admin/exercises/writing` |
| **39** | `[Admin] Writing_Part1_Management - /admin/exercises/writing/p1` | `1:2875` | 1722 x 836 px | Exercise CMS | `/admin/exercises/writing/p1` |
| **40** | `[Admin] Exams_Management - /admin/exams/lr` | `1:3237` | 1722 x 836 px | Exam CMS | `/admin/exams/lr` |
| **41** | `[Admin] Exams_Speaking_Management - /admin/exams/speaking` | `24:9700` | 1722 x 931 px | Exam CMS | `/admin/exams/speaking` |
| **42** | `[Admin] Exams_Writing_Management - /admin/exams/writing` | `24:9863` | 1722 x 931 px | Exam CMS | `/admin/exams/writing` |
| **43** | `[Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit` | `1:3475` | 1722 x 1952 px | Exam Builder | `/admin/exams/lr/[id]/edit` |
| **44** | `[Admin] Modal_Create_Exam_Metadata - Overlay` | `1:4227` | 1722 x 836 px | Exam CMS | Overlay Modal |

---

# 3. DETAILED SCREEN SPECIFICATIONS BY SUBSYSTEM

---

## SUBSYSTEM 1: LEARNER AUTHENTICATION (LEARNER AUTH)

### Screen 01: [Learner] Login_Page - /login (Frame ID: `1:2`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/login`.
- **Layout**: Split-screen at a 45 : 55 ratio.
- **Left Panel (Brand Showcase)**:
  - Warm background `#FAF7F5` with soft blurred gradient circles.
  - Brand logo `TOEICMaster` with slogan *"Learn your way"*.
  - Headline: *"Every conversation, one step forward."* (with "forward" styled in italics).
  - AI chat preview block: Bilingual language learning speech bubbles (Hello! - Greeting; Great! - Very good).
  - Metric Card: `+24% Progress this week` with a growth icon.
  - Testimonial Card: 5-star rating `★★★★★`, avatar `MT`, quote: *"I became confident communicating after just 8 weeks!"*.
- **Right Panel (Login Form)**:
  - Tab switcher: **[Sign In]** (Active - White background, bold text) | **[Sign Up]**.
  - Header: *"Welcome to TOEICMaster - Continue your journey"*.
  - Email Input: Placeholder `you@example.com`, label "Email".
  - Password Input: Includes Show/Hide Password eye icon.
  - Options: Checkbox *"Remember me"*; Link *"Forgot password?"* (`/forgot-password`).
  - Primary Button: **[SIGN IN]** Red-orange gradient (`#EC251B` → `#FF6C0D`).
  - Divider: *"or continue with"*; SSO Button: **[Continue with Google]**.
  - Footer: *"Don't have an account? Sign up for free"* (`/register`).

---

### Screen 02: [Learner] Register_Page - /register (Frame ID: `1:144`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/register`.
- **Layout**: Consistent split-screen layout matching the Login screen.
- **Registration Form**:
  - Tab switcher: **[Sign Up]** (Active).
  - Title: *"Welcome to TOEICMaster - Start a new journey"*.
  - Subtitle: *"Create a free account and start practicing speaking today."*.
  - Fields:
    1. Full name (`Nguyen Minh Anh`).
    2. Email (`you@example.com`).
    3. Password (`Minimum 8 characters`).
    4. Agreement checkbox: *"I agree to the Terms of Use"*.
  - Primary Button: **[Create Account]** (Red-orange gradient).
  - Footer: *"Already have an account? Sign in"* (`/login`).

---

### Screen 03: [Learner] Forgot_Password_Page - /forgot-password (Frame ID: `1:294`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/forgot-password`.
- **Form Details**:
  - Title: *"Account Recovery - Forgot your password?"*.
  - Instructions: *"Enter your registered email and we will send you a link to reset your password."*.
  - Input field: Single **Email** input (`you@example.com`).
  - Primary Button: **[Send Verification Code]** (Red-orange gradient).
  - Footer: *"Remembered your password? Back to sign in"* (`/login`).

---

## SUBSYSTEM 2: LEARNER PORTAL, PROFILE & PROGRESSION HISTORY (LEARNER PORTAL & PROGRESSION)

### Screen 04: [Learner] Home_Landing_Page - / (Frame ID: `13:67084`)

- **Dimensions**: `1722 x 1275 px`.
- **Route**: `/`.
- **Layout Type**: Modern landing page featuring Global Learner Header + Hero Banner + Feature Cards + CTA.
- **UI Components**:
  1. **Global Learner Header**:
     - Logo: `TOEICMaster`.
     - Navigation Menu: `Practice 15 Parts`, `ETS Mock Exams (3 Skills)`, `Achievement Leaderboard`, `Study Guide`.
     - User Action Group: When logged out: **[Sign In]** button and **[Sign Up Free]** button.
  2. **Guest Alert Banner (Trial Guest)**:
     - Light amber-orange banner: *"You are studying as a Guest. Your test results will not be saved to a personal profile after closing the browser."* with a **[Create Free Account]** button.
  3. **Hero Section**:
     - Identity badge: `4 SKILLS · 3 INDEPENDENT EXAMS`.
     - Headline: *"Vietnam's #1 ETS-Standard TOEIC Preparation Platform"*.
     - Subtitle: *"100% realistic virtual exam rooms, 200-question L&R test bank, detailed AI scoring for Speaking & Writing."*.
     - Dual CTA group: **[Take L&R Mock Test · 120 mins]** (Red-orange gradient) and **[Practice by Part]** (Orange border, transparent background).
     - Trust Badges: `✓ ETS-Standard Tests` · `✓ Bilingual Explanations` · `✓ Start for Free`.
  4. **3 Independent Exams Card Grid**:
     - Card 1: **Listening & Reading** (200 questions, 120 minutes, 990 scale, continuous Master Audio).
     - Card 2: **Speaking** (11 questions, 20 minutes, 200 scale, 2-phase Prep/Speak exam room).
     - Card 3: **Writing** (8 questions, 60 minutes, 200 scale, anti-cheat editor).

---

### Screen 05: [Learner] Profile_Settings_Page - /profile (Frame ID: `13:70116`)

- **Dimensions**: `1722 x 875 px`.
- **Route**: `/profile`.
- **UI Details**:
  - Breadcrumb: `Home / Personal Profile`.
  - Header: *"Profile & Study Goals - Personalize your journey to conquer TOEIC."*.
  - Personal Information Card:
    - Learner avatar: Initials `MA` (Minh Anh).
    - Meta info: Joined date `12/08/2026` · Total practice hours `48 hours 30 minutes`.
    - Input fields: Full name, Email, Phone number.
    - Button **[Save Changes]**.
  - Target Score Settings Card:
    - Listening & Reading Target: 10-990 scale (Example target score `850`).
    - Speaking Target: 0-200 scale (Example target score `160`).
    - Writing Target: 0-200 scale (Example target score `150`).
  - Security & Change Password Card:
    - Current Password, New Password (*"At least 8 characters, including uppercase, lowercase, and numbers"*), Confirm New Password.
    - Button **[Update Password]**.

---

### Screen 06: [Learner] History_Progression_Page - /history (Frame ID: `13:70205`)

- **Dimensions**: `1722 x 1985 px` (Long-scroll progression chart page).
- **Route**: `/history`.
- **Purpose**: Dashboard tracking test attempt history and score growth charts across attempts.
- **UI Details**:
  - Header: *"Every test attempt, one step forward - Track your results and look back on your journey toward your goal."*.
  - Monthly summary: `6 attempts · +170 points this month`.
  - 3 Tabs separated by the 3 independent exams:
    - **[1. Listening & Reading Exam History]** (990 scale - Active).
    - **[2. Speaking Exam History]** (200 scale).
    - **[3. Writing Exam History]** (200 scale).
  - Score Progression Line Chart:
    - Date range: `01/10/2026 - 31/10/2026 ⌄`.
    - Y-axis score range: 0 - 990 (Target line at 750 / 850).
    - Attempt milestones: Attempt 1 (650) → Attempt 3 (720) → Attempt 6 (820).
  - Detailed Exam Attempts Table:
    - Columns: `Attempt`, `Exam Name`, `Attempt Date`, `Completion Time`, `Listening / Reading Score`, `Total Score`, `Actions`.
    - Actions column: **[View Result]** (`/exams/lr/[id]/result/[attemptId]`) and **[Review Exam]** (`/exams/lr/[id]/review/[attemptId]`) buttons.

---

## SUBSYSTEM 3: LEARNER PRACTICE HUB

### Screen 07: [Learner] Practice_Hub_Overview - /practice (Frame ID: `13:67225`)

- **Dimensions**: `1722 x 965 px`.
- **Route**: `/practice`.
- **Purpose**: Central hub for selecting dedicated practice by individual Part with instant explanations.
- **UI Details**:
  - Header: *"Part-by-Part Practice Bank - Choose a skill and question type for focused training with answers and instant explanations."*.
  - 3 Skill Switcher Tabs:
    - **[Listening & Reading (Part 1-7)]** (Active).
    - **[Speaking (Part 1-5)]**.
    - **[Writing (Part 1-3)]**.
  - Part Cards Grid:
    - *Part 1: Photographs* (Photo description - 240 questions · Easy / Medium / Hard).
    - *Part 2: Question & Response* (Q&A - 300 questions).
    - *Part 3: Short Conversations* (Short conversations - 180 questions).
    - *Part 4: Short Talks* (Short talks - 160 questions).
    - *Part 5: Incomplete Sentences* (Sentence completion - 350 questions).
    - *Part 6: Text Completion* (Text completion - 140 questions).
    - *Part 7: Reading Comprehension* (Single & multiple passage reading comprehension - 220 questions).
  - Button on each card: **[Start Practicing →]**.

---

### Screen 08: [Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId] (Frame ID: `13:67472`)

- **Dimensions**: `1722 x 1000 px`.
- **Route**: `/practice/lr/[partId]`.
- **Purpose**: Interactive practice interface with immediate feedback upon selecting an answer.
- **UI Details**:
  - Top Control Bar:
    - Button **[← Back to Hub]**.
    - Identifier: `Part 3 · Conversations` | Position: `Question 4 / 20`.
    - Study toolkit: Font size controls `A−` / `A+`, Dropdown `Jump to Q04 ⌄`.
  - Advanced Audio Playback Controls:
    - Scrub bar `00:24 / 00:58`.
    - Rewind 5s `↶ 5s`, Play/Pause `▶`, Forward 5s `5s ↷`.
    - Playback speed selector: `0.8×` · `1.0×` · `1.2×`.
  - Conversation & Context Panel:
    - Topic title: `CONVERSATION · OFFICE RELOCATION` - *"A new home for our team"*.
    - Instructions: *"Listen to the conversation between two colleagues and answer the questions. You can replay, slow down speed, or loop section A-B."*.
  - Question & Instant Feedback Panel:
    - Multiple-choice options A, B, C, D.
    - Immediate feedback banner upon selection: `Instant Feedback · Selected B` (Green if correct, Red if incorrect).
    - Expandable Bilingual Explanation & Translation box below.

---

### Screen 09: [Learner] Practice_Speaking_Mode - /practice/speaking/[partId] (Frame ID: `13:67617`)

- **Dimensions**: `1722 x 850 px`.
- **Route**: `/practice/speaking/[partId]`.
- **Purpose**: Free-form speaking practice by Part (e.g., Part 1: Read a text aloud).
- **UI Details**:
  - Breadcrumb: `Home / Practice / Speaking / Part 1`.
  - Header: *"Read a text aloud - Practice pronunciation, stress, and intonation. Speak, play back, and improve with confidence."*.
  - Sample text passage to read aloud.
  - Tools to record a practice attempt and play back one's own voice.
  - Navigation buttons: **[← Back to Hub]** and **[Next Exercise →]**.

---

### Screen 10: [Learner] Practice_Writing_Mode - /practice/writing/[partId] (Frame ID: `13:67739`)

- **Dimensions**: `1722 x 982 px`.
- **Route**: `/practice/writing/[partId]`.
- **Purpose**: Practice writing sentences from pictures (Part 1), emails (Part 2), or essays (Part 3).
- **UI Details**:
  - Breadcrumb: `Home / Practice / Writing / Part 1`.
  - Header: *"Write a sentence based on a picture - Write one sentence describing the picture using both keywords. You may change the word forms if needed."*.
  - Exercise photo frame accompanied by 2 mandatory keyword prompts.
  - Answer text input box and submit button for automated grammar checking.

---

## SUBSYSTEM 4: ETS MOCK EXAMS LIBRARY HUB (LEARNER EXAMS LIBRARY HUB)

### Screen 11: [Learner] Exams_Library_Hub - /exams (Frame ID: `13:67810`)

- **Dimensions**: `1722 x 950 px`.
- **Route**: `/exams`.
- **Purpose**: Overview library routing into the 3 mock exam catalogs for the 3 independent exams.
- **UI Details**:
  - Header: *"ETS-Standard Mock Exam Library - Explore the 4-skill realistic exam bank organized into 3 independent exams."*.
  - 3 Large Exam Routing Banner Cards:
    1. **Listening & Reading**: 200 questions · 120 minutes · 990 scale · Button **[Enter L&R Exam List →]**.
    2. **Speaking**: 11 questions · ~20 minutes · 200 scale · Button **[Enter Speaking Exam List →]**.
    3. **Writing**: 8 questions · 60 minutes · 200 scale · Button **[Enter Writing Exam List →]**.

---

### Screen 12: [Learner] Exams_LR_List - /exams/lr (Frame ID: `24:8730`)

- **Dimensions**: `1722 x 918 px`.
- **Route**: `/exams/lr`.
- **UI Details**:
  - Breadcrumb: `Home / Mock Exams / Listening & Reading`.
  - Badge: `L&R · INDEPENDENT EXAM` (Blue `#EFF6FF` / `#1D4ED8`).
  - Title: *"Listening & Reading Exams - 120-minute continuous simulated exam room with ETS-standard Master Audio."*.
  - Learner metric: `HIGHEST SCORE: 855 / 990`.
  - Filter & Search Toolbar:
    - Input: *"Search L&R exam name..."*.
    - Dropdowns: `Status: All` (Completed / Not Attempted), `Difficulty: All`, `Sort: Newest`.
  - Exam Cards Grid:
    - Sample exam: `ETS 2024 · Full Test 01` (200 questions · 120 mins · 990 scale · Tag `✓ Completed` · Best: 855/990 · Button **[Retake]**).
    - Sample exam: `ETS 2024 · Full Test 02` (Tag `Not Attempted` · Button **[Start Exam]**).
  - Pagination: `Showing 1-6 of 24 exams`.

---

### Screen 13: [Learner] Exams_Speaking_List - /exams/speaking (Frame ID: `24:8867`)

- **Dimensions**: `1722 x 918 px`.
- **Route**: `/exams/speaking`.
- **UI Details**:
  - Breadcrumb: `Home / Mock Exams / Speaking`.
  - Badge: `SPEAKING · INDEPENDENT EXAM` (Purple `#FAF5FF` / `#7E22CE`).
  - Title: *"Speaking Exams - Practice speaking reflexes in a two-phase exam room and receive evaluation across 5 criteria."*.
  - Learner metric: `HIGHEST SCORE: 160 / 200`.
  - Sample exam list: `ETS Speaking · Test 01`, `Test 02`, `Test 03`... (Each exam: 11 questions · ~20 mins · 0-200 score · 5 Parts).

---

### Screen 14: [Learner] Exams_Writing_List - /exams/writing (Frame ID: `24:9318`)

- **Dimensions**: `1722 x 918 px`.
- **Route**: `/exams/writing`.
- **UI Details**:
  - Breadcrumb: `Home / Mock Exams / Writing`.
  - Badge: `WRITING · INDEPENDENT EXAM` (Green `#ECFDF5` / `#047857`).
  - Title: *"Writing Exams - Practice picture-based sentences, email responses, and opinion essays in 60 minutes."*.
  - Exam list: Each exam has 8 questions · 60 mins · 0-200 scale · 3 Parts.

---

## SUBSYSTEM 5: LISTENING & READING EXAM & EVALUATION FLOW (LEARNER L&R EXAM FLOW)

### Screen 15: [Learner] Exam_LR_Instructions - /exams/lr/[id] (Frame ID: `13:67968`)

- **Dimensions**: `1722 x 895 px`.
- **Route**: `/exams/lr/[id]`.
- **Purpose**: Exam rules and headphone sound-check page before starting the official exam timer.
- **UI Details**:
  - Title: `ETS 2024 · Full Test 01 - Listening & Reading Exam Instructions`.
  - Structure summary table: `200 questions` | `120 minutes` | `Section 1: Listening (100 questions - 45 minutes)` | `Section 2: Reading (100 questions - 75 minutes)`.
  - Sound Check Area: Play button for sample audio to test headphones.
  - Strict Exam Rules: Master Audio cannot be rewound; the countdown timer starts automatically upon entering the exam room.
  - Primary CTA Button: **[Start Exam Now →]** (Navigates to Screen 16).

---

### Screen 16: [Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt (Frame ID: `13:68036`)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ HEADER: [ETS 2024 · Test 01] | Listening in Progress (Part 3) | ⏰ Time Remaining: 01:14:28     │
│ BUTTONS: [⚑ Flag Question] [Pause (If allowed)]                            [SUBMIT EXAM]        │
├─────────────────────────────────────────────────────────────┬───────────────────────────────────┤
│ LEFT COLUMN (68%): QUESTION CONTENT & CONTEXT               │ RIGHT COLUMN (32%): 200-Q PALETTE │
│                                                             │ (QUESTION PALETTE - STICKY)       │
│ [PART 3: SHORT CONVERSATIONS] (Questions 32-34)             │ Answered: 34/200 · Flagged: 2     │
│ 🔊 Master Audio Playing: [━━━━━━●━━━━━] 18:24 / 45:00       ├───────────────────────────────────┤
│ (Rewind Disabled - Locked Listening Mode)                   │ Legend:                           │
│                                                             │ ■ Green: Answered □ White: Empty  │
│ QUESTION 32: What are the speakers mainly discussing?       │ ■ Orange: Active  ⚑ Yellow: Flag  │
│ ( ) A. A client presentation schedule                       ├───────────────────────────────────┤
│ (•) B. An upcoming office relocation                        │ LISTENING (1-100):                │
│ ( ) C. New software installation                            │ [1] [2] [3] [4] [5] [6] (Part 1)  │
│ ( ) D. Budget approval for marketing                        │ [7]...[31] (Part 2)               │
│                                                             │ [32][33][34]...[70] (Part 3)      │
│ QUESTION 33: What problem does the woman mention?           │ READING (101-200):                │
│ ( ) A...  ( ) B...                                          │ [101]...[130] (Part 5)            │
│ QUESTION 34: What will the man do next?                     │ [131]...[146] (Part 6)            │
│ ( ) A...  ( ) B...                                          │ [147]...[200] (Part 7)            │
└─────────────────────────────────────────────────────────────┴───────────────────────────────────┘
```

- **Dimensions**: `1722 x 902 px`.
- **Route**: `/exams/lr/[id]/attempt`.
- **Key Interactive Features**:
  1. **Exam Room Header**:
     - Monospace countdown timer `01:14:28` with real-time color warning thresholds.
     - Red-orange **[Submit Exam]** button at the top right.
  2. **Master Audio Mechanism (Listening Section)**:
     - Continuous 45-minute player without Pause/Seek controls (anti-scrubbing).
     - When the audio reaches the dialogue for questions 32-34, the viewport automatically focuses on that question cluster.
  3. **200-Question Palette**:
     - 200 square cells grouped across 7 Parts.
     - Cell turns green `#10B981` immediately when the learner selects an answer.
     - Cell shows a yellow flag icon `⚑` when flagged by the learner.
     - Clicking any Reading cell (101-200) scrolls directly to that question.

---

### Screen 17: [Learner] Modal_Exam_LR_Confirm_Submit - Overlay (Frame ID: `13:68592`)

- **Dimensions**: `1722 x 900 px` (Confirmation dialog displayed as an overlay dimming the exam room).
- **Purpose**: Warns about the count of unanswered questions before finalizing exam submission.
- **UI Details**:
  - Title: *"Confirm Listening & Reading Exam Submission"*.
  - Completion status summary:
    - `Completed`: **182 / 200 questions** (Green).
    - `Unanswered`: **18 questions** (Warning red-orange).
    - `Flagged for review`: **4 questions** (Yellow).
  - Warning: *"You still have 12 minutes remaining. Once submitted, you cannot change your selected answers."*.
  - 2 Action Buttons:
    - **[Continue Exam]** (Closes the modal and returns to the exam room).
    - **[Confirm & Submit Now]** (Submits to the server and redirects to Screen 18).

---

### Screen 18: [Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId] (Frame ID: `13:69428`)

- **Dimensions**: `1722 x 950 px`.
- **Route**: `/exams/lr/[id]/result/[attemptId]`.
- **Purpose**: Official score report on the ETS 990 scale after exam submission.
- **UI Details**:
  - Breadcrumb: `Home / Mock Exams / Listening & Reading / Attempt #01 Result`.
  - Hero Score Card:
    - Total ETS Score: Large figure `855 / 990` (`48px Bold`).
    - Section Scores:
      - **Listening**: `445 / 495` (89/100 correct).
      - **Reading**: `410 / 495` (83/100 correct).
    - Time Spent: `112 minutes 45 seconds`.
  - Part Breakdown Bars:
    - Displays accuracy percentages for each Part from Part 1 through Part 7.
  - Action Button Group:
    - **[Review Exam Details]** (Navigates to Screen 19 - Review Mode).
    - **[Retake This Exam]**.
    - **[Back to Exam List]**.

---

### Screen 19: [Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId] (Frame ID: `13:69536`)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ HEADER: [← Back to Score Report] | ETS 2024 · Test 01 · Exam Review (Attempt #01) · READ-ONLY   │
│ QUESTION FILTERS: [All (200)] [✓ Correct (172)] [✗ Incorrect (18)] [⊘ Skipped (10)]             │
├─────────────────────────────────────────────────────────────┬───────────────────────────────────┤
│ LEFT COLUMN (68%): CONTENT, TRANSCRIPT & EXPLANATION        │ RIGHT COLUMN (32%): REVIEW PALETTE│
│                                                             │                                   │
│ [PART 3: SHORT CONVERSATIONS] · Question 32                 │ Review Palette (200 questions)    │
│ Audio: [▶ 00:38] with Interactive Highlighted Transcript:   ├───────────────────────────────────┤
│ Man: Did you finish the quarterly report?                   │ Legend:                           │
│ Woman: [Not yet. There is a software error...] (Clue)       │ ■ Green: Correct (172)            │
│                                                             │ ■ Red: Incorrect (18)             │
│ QUESTION 32: What are the speakers mainly discussing?       │ ■ Gray: Skipped (10)              │
│ ( ) A. A new office computer                                ├───────────────────────────────────┤
│ [✓] B. The quarterly financial report [CORRECT ANSWER]      │ [1][2][3][4][5][6]...             │
│ [✗] C. A customer meeting [YOUR CHOICE - INCORRECT]         │ [32][33][34]...                   │
│                                                             │ (Cell 32 red w/ orange active bdr)│
│ DETAILED EXPLANATION BOX (BILINGUAL):                       │                                   │
│ - B is correct because the woman mentions the quarterly     │                                   │
│   report.                                                   │                                   │
│ - Clue is in the line: "Did you finish the quarterly..."    │                                   │
└─────────────────────────────────────────────────────────────┴───────────────────────────────────┘
```

- **Dimensions**: `1722 x 1050 px`.
- **Route**: `/exams/lr/[id]/review/[attemptId]`.
- **Dedicated UX Features**:
  - Read-only review mode.
  - Category filter bar: Allows filtering to only Incorrect (`18 questions`) or Skipped (`10 questions`) for quick learning from mistakes.
  - Interactive Transcript: Dialogue transcript highlights in yellow the exact lines serving as clues leading to the correct answer (Clue highlighting).
  - Color-coded Palette: Green (Correct), Red (Incorrect), Gray (Skipped).

---

## SUBSYSTEM 6: SPEAKING EXAM & EVALUATION FLOW (LEARNER SPEAKING EXAM FLOW)

### Screen 20: [Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check (Frame ID: `13:69176`)

- **Dimensions**: `1722 x 843 px`.
- **Route**: `/exams/speaking/[id]/device-check`.
- **Purpose**: Mandatory Microphone and Headphone hardware check before opening the Speaking exam room.
- **UI Details**:
  - Title: *"Microphone & Headphone Device Check - ETS Speaking Test 01"*.
  - Microphone Check Panel:
    - Dropdown to select the computer's microphone input source.
    - Button **[Speak to Test]**: Learner reads the sample sentence *"Hello, I am ready for the TOEIC Speaking test."*.
    - Real-time Volume VU Meter waveform oscillating with audio input.
    - Status badge: `✓ Microphone is working properly, volume level is optimal`.
  - Speaker/Headphone Check Panel: Button to play sample audio *"Click to test audio playback"*.
  - CTA Button: **[Devices Ready · Enter Speaking Exam Room →]** (Navigates to Screen 21).

---

### Screen 21: [Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt (Frame ID: `13:69278`)

- **Dimensions**: `1722 x 932 px`.
- **Route**: `/exams/speaking/[id]/attempt`.
- **Automated 2-Phase Execution**:
  1. **Header**:
     - Identifier: `ETS Speaking · Test 01 | Question 1 of 11 (Part 1: Read a text aloud)`.
     - Overall exam progress counter.
  2. **Prompt Display Area**:
     - Read-aloud text passage clearly displayed on a warm light background.
  3. **Two-Phase Control Block**:
     - **PHASE 1 (Preparation)**:
       - Banner: `PHASE 1 · MIC NOT RECORDING`.
       - Preparation countdown timer: `00:45` → `00:00`.
       - Static gray mic waveform, reminder text: *"Begin speaking after the Beep"*.
     - **PHASE TRANSITION**: The system automatically plays an ETS-standard "BEEP" notification sound.
     - **PHASE 2 (Response Recording)**:
       - Banner: `PHASE 2 · ● REC (RECORDING)`.
       - Speaking countdown timer: `00:45` → `00:00`.
       - Blinking red REC dot icon, live waveform oscillating with the learner's voice.
       - No re-recording allowed; when time expires, the system automatically saves the audio file and advances to the next question.

---

### Screen 22: [Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId] (Frame ID: `24:9004`)

- **Dimensions**: `1722 x 998 px`.
- **Route**: `/exams/speaking/[id]/result/[attemptId]`.
- **Purpose**: Speaking score report on the ETS 0-200 scale with feedback across 5 criteria.
- **UI Details**:
  - Title: *"SPEAKING · PERSONAL SCORE REPORT - Speaking Exam Result"*.
  - Total Score: `160 / 200` (ETS scale in 10-point increments).
  - Evaluation across 5 ETS-standard Speaking criteria:
    1. **Pronunciation**: `High / Good` (Clear vowels and consonants).
    2. **Intonation & Stress**: `High / Good` (Natural phrasing and pauses).
    3. **Grammar**: `Medium / Fair` (Some past-tense conjugation errors).
    4. **Vocabulary**: `High / Good` (Contextually appropriate word choice).
    5. **Cohesion & Relevance**: `High / Good`.
  - Action button: **[Replay Recordings & View Feedback]** (`/exams/speaking/[id]/review/[attemptId]`).

---

### Screen 23: [Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId] (Frame ID: `24:9106`)

- **Dimensions**: `1722 x 1000 px`.
- **Route**: `/exams/speaking/[id]/review/[attemptId]`.
- **UI Details**:
  - Header: *"Speaking · Test 01 · Exam Review · Attempt #02 · READ-ONLY"*.
  - 11-Question Speaking Palette: Allows selecting each question from Question 1 to Question 11.
  - Selected Question Detail Panel:
    - Original question prompt.
    - Audio player for the learner's actual recording: `00:36 / 00:45`.
    - Audio player for a native-level sample response for comparison.
    - Detailed AI/Expert feedback on strengths and mispronounced words needing improvement.

---

## SUBSYSTEM 7: WRITING EXAM & EVALUATION FLOW (LEARNER WRITING EXAM FLOW)

### Screen 24: [Learner] Exam_Writing_Instructions - /exams/writing/[id] (Frame ID: `24:9455`)

- **Dimensions**: `1722 x 864 px`.
- **Route**: `/exams/writing/[id]`.
- **Purpose**: Explains the 60-minute Writing exam room rules and anti-cheat policies.
- **UI Details**:
  - Title: `ETS Writing · Test 01 - Writing Exam Instructions`.
  - Structure: 8 questions in 60 minutes comprising:
    - *Part 1 (Questions 1-5)*: Write a sentence based on a picture (Target words).
    - *Part 2 (Questions 6-7)*: Respond to a written request (email).
    - *Part 3 (Question 8)*: Write an opinion essay (minimum 300 words).
  - Exam rule warning: The system locks copy-paste functionality from external sources into the text editor.
  - CTA Button: **[Start Writing Exam →]**.

---

### Screen 25: [Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt (Frame ID: `13:69370`)

- **Dimensions**: `1722 x 882 px`.
- **Route**: `/exams/writing/[id]/attempt`.
- **UI & Anti-Cheat Features**:
  - Header: 60-minute countdown timer `00:54:12` | Button **[Submit Exam]**.
  - 8-question tab navigation bar: `Q1` | `Q2` | `Q3` | `Q4` | `Q5` | `Q6` | `Q7` | `Q8`.
  - Left Prompt Panel (Picture / Email request / Essay prompt).
  - **Secure Text Editor on the Right**:
    - Completely blocks `paste` events and the right-click context menu.
    - Automatic word counter at bottom corner: *"320 words (Recommended 300-350 words)"*.
    - Navigation buttons: **[← Previous Question]** and **[Save & Next Question →]**.

---

### Screen 26: [Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId] (Frame ID: `24:9528`)

- **Dimensions**: `1722 x 900 px`.
- **Route**: `/exams/writing/[id]/result/[attemptId]`.
- **UI Details**:
  - Title: *"WRITING · PERSONAL SCORE REPORT - Writing Exam Result"*.
  - Estimated Total Score: `150 / 200` (ETS scale in 10-point increments).
  - Submission Summary: `8 / 8 questions submitted` · `320-word essay (Meets recommended length)` · `5/5 Part 1 responses used keywords properly`.
  - Detailed Breakdown:
    - Grammar & Sentence Structure score.
    - Vocabulary & Expression Variety score.
    - Logic & Essay Organization score.
  - Action button: **[Review Responses & Sample Answers]** (`/exams/writing/[id]/review/[attemptId]`).

---

### Screen 27: [Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId] (Frame ID: `24:9605`)

- **Dimensions**: `1722 x 1478 px` (Response review and sample essay comparison view).
- **Route**: `/exams/writing/[id]/review/[attemptId]`.
- **UI Details**:
  - Header: *"ETS Writing · Test 01 · Attempt #02 · FINAL SUBMISSION · READ-ONLY"*.
  - Navigation across all 8 questions (Part 1, Part 2, Part 3).
  - Side-by-side comparison Split-view:
    - Column 1: Question prompt and exam requirements.
    - Column 2: Full verbatim response submitted by the learner.
    - Column 3: Top-scoring sample response (high Band Score) with grammar analysis, advanced sentence structures, and synonym suggestions.

---

## SUBSYSTEM 8: ADMIN MANAGEMENT - OVERVIEW & USERS (ADMIN CORE)

### Screen 28: [Admin] Overview_Dashboard - /admin/overview (Frame ID: `1:823`)

- **Dimensions**: `1722 x 1200 px`.
- **Route**: `/admin/overview`.
- **Purpose**: Executive operations monitoring dashboard for Administrators.
- **Data Structure**:
  1. Header & Meta: Breadcrumb `Admin / Overview` · Timestamp `Updated: 19/10/2026, 10:30`.
  2. 4 Operational KPI Cards:
     - *Total Users*: `3,250` (`+120 new registrations this week`).
     - *Locked Accounts*: `14` (`Access temporarily suspended`).
     - *Exercise Bank*: `1,940 items` (Covering all 15 Parts across 4 skills).
     - *Mock Exams*: `28 exams` (`20 Published / 8 Draft`).
  3. Breakdown Charts Section:
     - Stacked Bar Chart of question distribution: Listening 800, Reading 680, Speaking 270, Writing 190.
     - Donut Chart of exam status: 20 Published (71%) / 8 Draft (29%).
  4. Recent Activity Timeline:
     - Real-time log of Admin and Operator actions.
     - System Health Badge: *"System operating normally"*.

---

### Screen 29: [Admin] Users_Management - /admin/users (Frame ID: `1:393`)

- **Dimensions**: `1722 x 871 px`.
- **Route**: `/admin/users`.
- **UI Details**:
  - Title: `User Management (64 accounts)` with button **[+ Add User]**.
  - Status filter tabs: **[All (64)]** | **[Active (45)]** | **[Pending Verification (11)]** | **[Locked (8)]**.
  - Filter Toolbar: Name/email search input, Role filter dropdown, Status filter dropdown, **[Export CSV]** button.
  - User Data Table:
    - Columns: `User ↕`, `Role ↕`, `Status ↕`, `Last Login ↕`, `ACTIONS`.
    - Actions: View Details (Eye icon), Lock/Unlock Account (Padlock icon).
  - Pagination: `Showing 1 - 7 of 64 users`.

---

### Screen 30: [Admin] Users_Loading_State - /admin/users?state=loading (Frame ID: `13:70854`)

- **Dimensions**: `1722 x 948 px`.
- **Route**: `/admin/users?state=loading`.
- **Purpose**: Specifies the table loading state (Skeleton Loading State) following UX standards to prevent layout shift during API fetches.
- **UI Details**:
  - Preserves the Sidebar and Header layout.
  - All Data Table rows and count badges are replaced with rounded light-gray Skeleton bars with a pulsing Shimmer effect.

---

### Screen 31: [Admin] Users_Empty_State - /admin/users?state=empty (Frame ID: `13:70775`)

- **Dimensions**: `1722 x 908 px`.
- **Route**: `/admin/users?state=empty`.
- **Purpose**: Specifies the empty-results state when applying filters or search queries.
- **UI Details**:
  - The data table area displays a friendly Empty Illustration.
  - Message: *"No users match the current filters."*.
  - Secondary CTA Button: **[Reset Filters]** to restore the table to showing all 64 accounts.

---

## SUBSYSTEM 9: ADMIN MANAGEMENT - 4-SKILL EXERCISE BANK (EXERCISE BANK CMS)

### Screen 32: [Admin] Exercises_LR_Parts - /admin/exercises/lr (Frame ID: `1:1116`)

- **Dimensions**: `1722 x 915 px`.
- **Route**: `/admin/exercises/lr`.
- **UI Details**:
  - Level-1 Tabs: **[Exercises]** (Active) | **[Exams]**.
  - Level-2 Tabs: **[Listening & Reading (7 Parts)]** (Active) | **[Speaking (5 Parts)]** | **[Writing (3 Parts)]**.
  - Summary stats: `Total exercises: 1,248 | Updated: 17/10/2026`.
  - 7-Part Management Grid: Part 1 Photographs (186 items), Part 2 Question-Response (240 items), Part 3 Short Conversations (174 items), Part 4 Short Talks (158 items), Part 5 Incomplete Sentences (226 items), Part 6 Text Completion (132 items), Part 7 Reading Comprehension (132 passage clusters).

---

### Screen 33: [Admin] Listening_Part1_Management - /admin/exercises/lr/p1 (Frame ID: `1:1389`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exercises/lr/p1`.
- **UI Details**:
  - Manages the list of Part 1 (Photographs) questions.
  - Question table: ID, Thumbnail image preview, Audio player, Transcript for options A-B-C-D with a `KEY` badge on the correct answer, Difficulty, Status.
  - Add New Question Dialog: Image upload field (JPG/PNG max 5MB), MP3 upload field (max 10MB), 4 radio buttons to select the correct answer, Vietnamese explanation input field.

---

### Screen 34: [Admin] Reading_Part7_Management - /admin/exercises/lr/p7 (Frame ID: `13:70307`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exercises/lr/p7`.
- **Purpose**: Dedicated management screen for the hardest and longest TOEIC Reading part: Reading Comprehension.
- **UI Details**:
  - Title: *"Part 7: Reading Comprehension - Manage 128 passage clusters · 452 sub-questions · Single, Double & Triple Passages"*.
  - Action buttons: **[+ Add Passage Cluster]** and **[Import from Excel]**.
  - Passage Cluster Management Table:
    - Columns: `Cluster ID`, `Passage Type (Single / Double / Triple)`, `Passage Excerpt`, `Sub-Question Count`, `Topic`, `Difficulty`, `Actions`.

---

### Screen 35: [Admin] Modal_Add_Reading_Part7_Cluster - Overlay (Frame ID: `13:70476`)

- **Dimensions**: `1722 x 1100 px` (Overlay Modal).
- **Purpose**: Complex dialog for configuring a Part 7 reading cluster with multiple passages and associated sub-questions.
- **Form Details**:
  - Passage structure selector: `Single Passage (2 to 4 questions)`, `Double Passage (5 questions)`, `Triple Passage (5 questions)`.
  - Rich-text editor for entering 1, 2, or 3 reading passages (emails, articles, invoices, etc.).
  - Sub-question list builder: Each question includes prompt text, 4 multiple-choice options A-B-C-D, correct answer radio button, and explanation.

---

### Screen 36: [Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1 (Frame ID: `1:1804`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exercises/speaking/p1`.
- **UI Details**:
  - Title: *"Speaking Part 1: Read a text aloud - Manage 85 pronunciation, stress, and intonation exercises (50-70 word passages)"*.
  - Management table: Passage excerpt, Prep time (45s), Speaking time (45s), Sample audio file preview, Status.
  - Edit modal: Provides automatic word counter, sample audio upload `sp1_sample_01.mp3`, and pronunciation guide notes for difficult words.

---

### Screen 37: [Admin] Modal_Import_Exercises_Excel - Overlay (Frame ID: `1:2218`)

- **Dimensions**: `1722 x 836 px` (Overlay Modal).
- **Purpose**: Dialog for uploading Excel (.xlsx) files to batch-import exercises.
- **UI Details**:
  - Banner to download the standardized Excel template file.
  - Drag-and-drop Dropzone supporting `.xlsx` and `.xls` formats up to 10MB.
  - Processing options: Checkboxes *"Auto-activate questions after import"* and *"Skip duplicate IDs"*.
  - Buttons **[Cancel]** and **[Start Upload]**.

---

### Screen 38: [Admin] Writing_Parts - /admin/exercises/writing (Frame ID: `1:2674`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exercises/writing`.
- **UI Details**:
  - Skill tab: **[Writing (3 Parts)]**.
  - 3-Part Grid:
    - *Part 1: Write a Sentence* (108 exercises - Picture-based sentence writing).
    - *Part 2: Respond to a Request* (76 exercises - Email response).
    - *Part 3: Write an Opinion Essay* (54 exercises - Opinion essay writing).

---

### Screen 39: [Admin] Writing_Part1_Management - /admin/exercises/writing/p1 (Frame ID: `1:2875`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exercises/writing/p1`.
- **UI Details**:
  - Manages sentence-writing exercises based on an image and 2 mandatory keywords.
  - Add-new modal: Upload prompt image, enter Keyword 1, enter Keyword 2, list equivalent full-score sample sentences, grammar grading criteria.

---

## SUBSYSTEM 10: ADMIN MANAGEMENT - EXAMS & EXAM BUILDER (EXAM BUILDER CMS)

### Screen 40: [Admin] Exams_Management - /admin/exams/lr (Frame ID: `1:3237`)

- **Dimensions**: `1722 x 836 px`.
- **Route**: `/admin/exams/lr`.
- **UI Details**:
  - Manages the Listening & Reading mock exam catalog.
  - Exams table: `Exam Code`, `Exam Name`, `Duration (120 mins)`, `Question Count (200 questions)`, `Status (Published / Draft)`.
  - Action buttons: **[+ Create New Exam]** (Opens Modal 44) and **[Import Exam from Excel]**.

---

### Screen 41: [Admin] Exams_Speaking_Management - /admin/exams/speaking (Frame ID: `24:9700`)

- **Dimensions**: `1722 x 931 px`.
- **Route**: `/admin/exams/speaking`.
- **Purpose**: Newly added screen dedicated to managing independent Speaking mock exams.
- **UI Details**:
  - Sub-tab: **[Speaking]** active under the Exams menu.
  - Header: *"Speaking Exam List | 11 questions · ~20 mins · 200 Scale"*.
  - CTA Button: **[+ Create New Speaking Exam]**.
  - Exam management table: Exam Code (`SP-2024-01`), Exam Name, Slot Count (`11/11 · Complete`), Scale (`0-200`), Status (`Published / Draft`).

---

### Screen 42: [Admin] Exams_Writing_Management - /admin/exams/writing (Frame ID: `24:9863`)

- **Dimensions**: `1722 x 931 px`.
- **Route**: `/admin/exams/writing`.
- **Purpose**: Newly added screen dedicated to managing independent Writing mock exams.
- **UI Details**:
  - Sub-tab: **[Writing]** active under the Exams menu.
  - Header: *"Writing Exam List | 8 questions · 60 mins · 200 Scale"*.
  - CTA Button: **[+ Create New Writing Exam]**.
  - Exam management table: Exam Code (`WR-2024-01`), Exam Name, Slot Count (`8/8 · Complete` or `5/8 · Incomplete`), Scale (`0-200`), Status (`Published / Draft`).

---

### Screen 43: [Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit (Frame ID: `1:3475`)

- **Dimensions**: `1722 x 1952 px`.
- **Route**: `/admin/exams/lr/[id]/edit`.
- **Master-Detail Layout**:
  - **Top Bar**: Exam Code `ETS-2024-TEST-01`, `Draft` badge, Progress `145/200 questions completed (72.5%)`, Button **[Publish Exam]** (Disabled until all 200 questions are complete).
  - **Validation Banner**: Detailed error location alerts: *"Part 3 missing audio for question 36; Part 7 missing answer key for question 180. Complete 45 empty slots and fix 10 error slots."*. Clicking an error automatically scrolls to that question.
  - **Left Column (68% - Editor)**: Author single questions or question clusters sharing audio/passages.
  - **Right Column (32% - Sticky Palette)**: 200-cell grid divided across 7 Parts, updating in real time based on the Admin's input status.

---

### Screen 44: [Admin] Modal_Create_Exam_Metadata - Overlay (Frame ID: `1:4227`)

- **Dimensions**: `1722 x 836 px` (Overlay Modal).
- **Purpose**: Step 1 to initialize exam shell metadata before redirecting to the 200-slot exam builder.
- **Form Details**:
  - Title: *"Create New Exam"*.
  - Exam Type: Tab selection among **[Listening & Reading]**, **[Speaking]**, or **[Writing]**.
  - Fields: Exam Code (`ETS-2025-TEST-01`), Exam Name, Exam Duration (minutes), Score Conversion Scale (Default ETS), Source Description.
  - Button: **[Continue to Question Assembly]** (Navigates to Exam Builder).

---

# 4. UX/UI PROFESSIONAL ASSESSMENT & DESIGN EVOLUTION COMPARISON

### 4.1. Comparison of the Major Leap Between V1.0 and Current Version (V2.1)

| Comparison Criterion | Legacy Version (V1.0 - October 2026) | Current Version (V2.1 - November 2026) | UX/UI Impact Assessment |
| :--- | :--- | :--- | :--- |
| **Screen Count & Scope** | Only 14 rudimentary screens | **44 complete screens** + **Design System Page** | 100% User Journey coverage from Learners to Admins |
| **Naming Convention** | Ad-hoc names with spelling errors (*"nguoi dung"* [typo], *"Writinig"*) | Fully standardized: `[Role] Name - /route` | FE developers know the exact route path from the frame name |
| **Exam Architecture** | Mixed all 4 skills into a single test | Standardized **3 Independent Exams**: L&R, Speaking, Writing | 100% aligned with official IIG/ETS international testing format |
| **Learner App Subsystem** | Completely undesigned | Complete from Landing, Profile, History, Practice to Virtual Exam Rooms | Smooth, cohesive study and test-taking experience for learners |
| **L&R Virtual Exam Room** | Only had Admin authoring pages | 120-min room, Locked audio, Question Palette, Confirm Modal | Realistic mock exam experience closely matching actual test rooms |
| **Speaking & Writing Rooms** | Non-existent | Device check, Two-phase recorder (Beep sound), Anti-cheat editor | Fully solves the high-interaction mock testing problem |
| **UI System States** | Missing Empty State and Loading State | Complete Skeleton Shimmer and illustrated Empty State | Elevates perceived product polish and completeness |
| **Design System** | Scattered across artboards | Dedicated `11:2` page with Colors, Typo, Master components | Enables the Frontend team to build the UI Component Library rapidly |

### 4.2. Key Design Highlights

1. **Two-Phase Speaking Flow (Preparation → Beep → REC)**: Faithfully replicates the IIG/ETS computer-based exam room 100%, helping learners avoid psychological surprises during the real exam.
2. **Anti-Cheat Writing Editor**: Disabling external paste and providing a real-time word counter trains learners in thinking and typing directly in English.
3. **Interactive Review Mode with Clue Highlighting**: Highlighting clue dialogue lines in the transcript helps learners understand why they got a question wrong rather than only seeing plain A/B/C/D answer keys.

---

# 5. FRONTEND IMPLEMENTATION & CONTRACT INTEGRATION GUIDE

Based on the detailed specification above, the **Frontend Developer** team should follow these technical standards when implementing code in the `TOEICMaster-FE/` repository:

1. **Initialize Next.js App Router Directory Routes**:
   - `app/(auth)/login/page.tsx`
   - `app/(auth)/register/page.tsx`
   - `app/(auth)/forgot-password/page.tsx`
   - `app/(learner)/page.tsx` (Home Landing Page)
   - `app/(learner)/profile/page.tsx`
   - `app/(learner)/history/page.tsx`
   - `app/(learner)/practice/page.tsx`
   - `app/(learner)/practice/lr/[partId]/page.tsx`
   - `app/(learner)/practice/speaking/[partId]/page.tsx`
   - `app/(learner)/practice/writing/[partId]/page.tsx`
   - `app/(learner)/exams/page.tsx`
   - `app/(learner)/exams/lr/page.tsx` & `app/(learner)/exams/lr/[id]/attempt/page.tsx` & `review/[attemptId]/page.tsx`
   - `app/(learner)/exams/speaking/page.tsx` & `[id]/device-check/page.tsx` & `[id]/attempt/page.tsx`
   - `app/(learner)/exams/writing/page.tsx` & `[id]/attempt/page.tsx` & `review/[attemptId]/page.tsx`
   - `app/admin/overview/page.tsx`
   - `app/admin/users/page.tsx`
   - `app/admin/exercises/lr/page.tsx` & `lr/[partId]/page.tsx`
   - `app/admin/exercises/speaking/[partId]/page.tsx`
   - `app/admin/exercises/writing/[partId]/page.tsx`
   - `app/admin/exams/lr/page.tsx` & `lr/[id]/edit/page.tsx`
   - `app/admin/exams/speaking/page.tsx`
   - `app/admin/exams/writing/page.tsx`
2. **Configure Design Tokens in `tailwind.config.ts`**:
   - Declare all brand colors (`#FF5A1F`, `#EC251B`), exam category colors (Blue `#1D4ED8`, Purple `#7E22CE`, Green `#047857`), and Question Palette colors.
   - Load Google Fonts `Poppins` with weights 400, 500, 600, 700.
3. **State Management & Real-Time Synchronization**:
   - For the L&R exam room: Ensure Master Audio plays via the HTML5 Web Audio API synchronized with the Question Palette, persisting learner answers to LocalStorage/IndexedDB to safeguard against network interruptions.
   - For the Speaking exam room: Use `MediaRecorder API` to record `.webm` / `.mp3` audio, and render waveforms using `AudioContext AnalyserNode`.
   - For the Writing exam room: Intercept `onPaste={(e) => e.preventDefault()}` and calculate word count via regex `text.trim().split(/\s+/).length`.

---

> **CONCLUSION & APPROVAL FROM PRODUCT DESIGN LEAD:**  
> All 44 screens in the latest Figma design and the Design System Foundations of **TOEICMaster** have been uniformly standardized to a Production-Ready level. This detailed specification serves as the authoritative reference for development, testing, and product acceptance.
