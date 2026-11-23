# DETAILED PRODUCT REQUIREMENTS DOCUMENT (PRD / SRS)
## ONLINE TOEIC MOCK TEST SYSTEM (TOEICMASTER - MVP)
### INDEPENDENT MOCK TEST SUBSYSTEM SPECIFICATIONS: LISTENING & READING, SPEAKING, AND WRITING

---

| **Project Information** | **Details** |
| :--- | :--- |
| **Project Name** | **TOEICMaster** - Online TOEIC Practice & Proficiency Assessment Platform |
| **Document Version** | **2.1** (Updated business standardization: Fully separate 3 independent mock test flows: L&R, Speaking, Writing) |
| **Author Role** | Senior EdTech Business Analyst (Lead BA) |
| **Target Audience** | UI/UX Designer, Frontend Engineer, Backend Engineer, QA/QC Tester, Product Owner |
| **Version Goal** | Detailed specification of all MVP features for UI/UX design and technical implementation |
| **Document Status** | **Approved for Design & Sprint Implementation** |

---

## TABLE OF CONTENTS

1. [PROJECT OVERVIEW & MVP STRATEGY (EXECUTIVE SUMMARY)](#1-project-overview--mvp-strategy)
2. [ROLES & PERMISSIONS SYSTEM (ACTOR & RBAC SPECIFICATIONS)](#2-roles--permissions-system)
3. [INFORMATION ARCHITECTURE & NAVIGATION SITEMAP (IA & SITEMAP)](#3-information-architecture--navigation-sitemap)
4. [LEARNER APP FUNCTIONAL SPECIFICATIONS (LEARNER APP)](#4-learner-app-functional-specifications)
   - 4.1. Authentication, Guest & Profile Flows (Auth, Guest & Profile)
   - 4.2. Skill & Part-based Practice Mode Subsystem
   - 4.3. ETS-Standard Independent Mock Test Engines Subsystem
     - 4.3.0. Independent Mock Test Organization Principles & Test-Taking Lifecycle
     - 4.3.1. Independent TOEIC Listening & Reading Test (200 questions - 120 minutes)
     - 4.3.2. Independent TOEIC Speaking Test (11 questions - ~20 minutes)
     - 4.3.3. Independent TOEIC Writing Test (8 questions - 60 minutes)
   - 4.4. Separate Results & Review Modes Subsystem
     - 4.4.1. Result Report & Review Mode for L&R Test (ETS 990 Scale)
     - 4.4.2. Result Report & Review Mode for Speaking Test (ETS 200 Scale)
     - 4.4.3. Result Report & Review Mode for Writing Test (ETS 200 Scale)
   - 4.5. Attempt History & Skill-based Progress Tracking Subsystem (History & Progress)
5. [ADMIN PORTAL FUNCTIONAL SPECIFICATIONS (ADMIN PORTAL)](#5-admin-portal-functional-specifications)
   - 5.1. Admin Overview Dashboard
   - 5.2. User Management & Permissions (User Management)
   - 5.3. Part-based Exercise & Standalone Question Bank (Exercise Bank)
   - 5.4. TOEIC Listening & Reading Exam Management (Exam Management - L&R)
   - 5.5. TOEIC Speaking & Writing Exam Management (Exam Management - S&W)
   - 5.6. Media Asset Management
   - 5.7. ETS-Standard Score Profiles Management (Score Profiles)
6. [UX/UI DESIGN DIRECTIVES FOR DESIGNERS (UX/UI DESIGN DIRECTIVES)](#6-uxui-design-directives-for-designers)
   - 6.1. Core EdTech User Experience Principles
   - 6.2. Design of Specific UI Components
   - 6.3. UI States Specification
   - 6.4. Responsive & Breakpoints Guidelines
7. [NON-FUNCTIONAL REQUIREMENTS & SYSTEM SAFETY (NFR & EDGE CASES)](#7-non-functional-requirements--system-safety)
8. [OVERALL ACCEPTANCE CRITERIA MATRIX (ACCEPTANCE CRITERIA MATRIX)](#8-overall-acceptance-criteria-matrix)

---

# 1. PROJECT OVERVIEW & MVP STRATEGY

### 1.1. Context & Business Nature of TOEIC Testing
TOEIC (Test of English for International Communication) covers 4 skills: Listening, Reading, Speaking, and Writing.
However, according to the international standard test organization structure of Educational Testing Service (ETS) and practical habits of learners in Vietnam:
1. **Listening & Reading (L&R) Test:** A paper/computer-based multiple-choice test combining Listening and Reading skills, lasting **120 minutes (200 questions)** with a combined converted score scale of **10 - 990 points**.
2. **Speaking Test:** An independent computer-based Speaking proficiency test, lasting **~20 minutes (11 questions)** with a separate converted score scale of **0 - 200 points**.
3. **Writing Test:** An independent computer-based Writing proficiency test, lasting **60 minutes (8 questions)** with a separate converted score scale of **0 - 200 points**.

> [!IMPORTANT]
> **CORE LEARNER TEST-TAKING BUSINESS DIRECTION:**
> The system **DOES NOT require and DOES NOT conduct a combined 4-skill exam within a single test session**.
> When entering the platform, learners have full freedom to choose to take:
> - **A separate TOEIC Listening & Reading test** (Taken separately, timed separately for 120 minutes, submitted separately, scored on the 990 scale).
> - **A separate TOEIC Speaking test** (Taken separately, timed per question across Preparation/Speaking phases, submitted separately, scored on the 200 scale).
> - **A separate TOEIC Writing test** (Taken separately, timed separately for 60 minutes, submitted separately, scored on the 200 scale).
> Every process from creating a test session (`Attempt`), countdown timer, submission, scoring, and detailed review (`Review Mode`) runs **completely independently** for each test type.

### 1.2. MVP (Minimum Viable Product) Goals
- **Pillar 1 (Learner - Flexible Part-based Practice):** Independent practice by each Part across the 15 Parts of the 4 skills.
  Includes instant right/wrong feedback, transcripts, translations, and detailed explanations.
- **Pillar 2 (Learner - 3 ETS-Standard Independent Mock Tests):**
  - L&R Exam Room (200 questions, 120 minutes, Master Audio with no seek/pause, Question Palette, automatic scoring on the 990 Scale).
  - Speaking Exam Room (11 questions, 2 timing phases, automatic recording, scoring on the 200 Scale).
  - Writing Exam Room (8 questions, 60 minutes, anti-copy-paste, spellcheck=false, scoring on the 200 Scale).
- **Pillar 3 (Admin - Data Control & Governance):** User management (Lock/Unlock), Part-based exercise bank management, creation and publishing of 3 independent exam types, L&R exam import via Excel XLSX, Media library management (Images/Audio), and ETS-standard score profiles.

### 1.3. MVP Scope Table (In-Scope vs. Out-of-Scope)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TOEICMASTER PROJECT SCOPE                         │
├──────────────────────────────────────────────┬──────────────────────────────┤
│              IN-SCOPE (MVP)                  │     OUT-OF-SCOPE (PHASE 2)   │
├──────────────────────────────────────────────┼──────────────────────────────┤
│ 1. Register, login via email + OTP, Google.  │ 1. Competitive arena         │
│ 2. Guest trial mode with warning banner.     │    (1vs1 Battle mode).       │
│ 3. Personal Profile & Attempt History.       │ 2. Social network, friends,  │
│ 4. Practice by individual Part (15 Parts).   │    chat.                     │
│ 5. INDEPENDENT Mock Tests:                   │ 3. Nested Notion-like        │
│    - Separate Listening & Reading test.      │    notebooks.                │
│    - Separate Speaking test.                 │ 4. Resource marketplace,     │
│    - Separate Writing test.                  │    e-commerce for courses.   │
│ 6. Separate score profiles per test type     │ 5. Open-ended AI Chatbot     │
│    (L&R: 990 scale; Speaking: 200 scale;     │    for arbitrary topics.     │
│     Writing: 200 scale).                     │ 6. Deep Spaced Repetition    │
│ 7. Separate Review Mode per test type.       │    Flashcard system.         │
│ 8. Admin: User management, Lock/Unlock.      │ 7. Payment gateways          │
│ 9. Admin: Part exercises & 3 exam types.     │    (VNPAY, MoMo, Stripe).    │
│ 10. Admin: Import L&R exams via Excel XLSX.  │                              │
└──────────────────────────────────────────────┴──────────────────────────────┘
```

---

# 2. ROLES & PERMISSIONS SYSTEM

### 2.1. System Actors
1. **Guest / Anonymous User:**
   - Users visiting the website without logging in.
   - Can browse the exam catalog, try sample Part exercises, or take 1 independent mock test (using a temporary session identifier `X-Anonymous-Session`).
   - Displays a Sticky Banner warning that results are not permanently saved and inviting account registration.
2. **Learner / Authenticated Member:**
   - Authenticated via Email + OTP or Google Account.
   - Allowed to choose and take any exam: L&R Exam, Speaking Exam, or Writing Exam.
   - Exam history and scores are permanently stored across the 3 skills; allowed to review detailed submissions (Review Mode) with transcripts, detailed explanations, sample audio, and model essays.
3. **Admin / System Administrator:**
   - Privileged account assigned the `Admin` role.
   - Full access to Admin Portal (`/admin`): Manage users, create and edit exams/exercises by skill, import Excel files, upload media files, configure ETS score profiles.
4. **Automated System / Background Worker:**
   - Manages independent real-time timers for each test session (`Attempt`), auto-locks tests upon timeout, calculates scores using the corresponding Score Profile, processes Writing/Speaking evaluation queues, and cleans up temporary audio files.

### 2.2. Role-Based Access Control (RBAC) Matrix

| Feature Group / Action | Guest | Learner | Admin |
| :--- | :---: | :---: | :---: |
| View home page, exam structure, exam catalog | ✅ Yes | ✅ Yes | ✅ Yes |
| Practice by individual Part (Practice Mode) | ✅ Yes (temporary) | ✅ Yes | ✅ Yes (Preview) |
| Select and take separate **Listening & Reading** exam | ✅ Yes (Data loss warning) | ✅ Yes (History saved) | ✅ Yes (Test Mode) |
| Select and take separate **Speaking** exam | ✅ Yes (Data loss warning) | ✅ Yes (History saved) | ✅ Yes (Test Mode) |
| Select and take separate **Writing** exam | ✅ Yes (Data loss warning) | ✅ Yes (History saved) | ✅ Yes (Test Mode) |
| Receive result report & separate score per exam | ✅ Yes | ✅ Yes | ✅ Yes |
| Detailed submission review (Separate Review Mode) | ⚠️ Limited sample questions | ✅ Yes | ✅ Yes |
| Save exam history across 3 tabs (L&R, Speaking, Writing) | ❌ No | ✅ Yes | ✅ Yes |
| Update profile, change password, view personal progress | ❌ No | ✅ Yes | ✅ Yes |
| Access Admin Portal `/admin` | ❌ No (403/401) | ❌ No (403/401) | ✅ Yes |
| Manage user accounts (Lock / Unlock) | ❌ No | ❌ No | ✅ Yes |
| Create, edit, publish Exams & standalone Exercises | ❌ No | ❌ No | ✅ Yes |
| Upload Media Assets & manage ETS Score Profiles | ❌ No | ❌ No | ✅ Yes |
| Import L&R exams from Excel XLSX files | ❌ No | ❌ No | ✅ Yes |

---

# 3. INFORMATION ARCHITECTURE & NAVIGATION SITEMAP

### 3.1. Learner App Sitemap
The Exam Library page (`/exams`) is organized into **3 independent exam sections**:

```
[Home Page - Landing Page] (/)
 ├── [Login / Register / Forgot Password] (/login, /register, /forgot-password)
 ├── [Personal Profile] (/profile)
 ├── [Attempt History - 3 Sections] (/history)
 │    ├── Listening & Reading Exam History (/history/lr)
 │    ├── Speaking Exam History (/history/speaking)
 │    └── Writing Exam History (/history/writing)
 ├── [PRACTICE BY PART] (/practice)
 │    ├── L&R Practice (Part 1 - Part 7) (/practice/lr/[partId])
 │    ├── Speaking Practice (Part 1 - Part 5) (/practice/speaking/[partId])
 │    └── Writing Practice (Part 1 - Part 3) (/practice/writing/[partId])
 └── [MOCK TEST EXAM CENTER] (/exams)
      │
      ├── [1] LISTENING & READING EXAM SECTION (/exams/lr)
      │    ├── L&R Exam List (Filter: Attempted, Unattempted, Difficulty) (/exams/lr)
      │    ├── L&R Exam Details & Instructions (/exams/lr/[examId])
      │    ├── Online L&R Exam Room (200 questions, 120 minutes) (/exams/lr/[examId]/attempt)
      │    ├── L&R Exam Result Report (990 Scale) (/exams/lr/[examId]/result/[attemptId])
      │    └── Detailed L&R Exam Review (Review Mode) (/exams/lr/[examId]/review/[attemptId])
      │
      ├── [2] SPEAKING EXAM SECTION (/exams/speaking)
      │    ├── Speaking Exam List (/exams/speaking)
      │    ├── Microphone & Headset Check (/exams/speaking/[examId]/device-check)
      │    ├── Speaking Exam Room (11 questions, 2 timing phases) (/exams/speaking/[examId]/attempt)
      │    ├── Speaking Exam Result Report (200 Scale) (/exams/speaking/[examId]/result/[attemptId])
      │    └── Review & Replay Speaking Exam (/exams/speaking/[examId]/review/[attemptId])
      │
      └── [3] WRITING EXAM SECTION (/exams/writing)
           ├── Writing Exam List (/exams/writing)
           ├── Writing Exam Details & Instructions (/exams/writing/[examId])
           ├── Writing Exam Room (8 questions, 60 minutes) (/exams/writing/[examId]/attempt)
           ├── Writing Exam Result Report (200 Scale) (/exams/writing/[examId]/result/[attemptId])
           └── Review & Read Writing Feedback (/exams/writing/[examId]/review/[attemptId])
```

### 3.2. Admin Portal Sitemap
Admin management for exams and exercises is distributed into corresponding independent modules:

```
[Admin Portal] (/admin)
 ├── [Operations Overview] (/admin/overview)
 ├── [User Management] (/admin/users)
 │    ├── Account List (/admin/users)
 │    └── User Details & 3-Skill Exam History (/admin/users/[userId])
 ├── [Part-Based Exercise Bank Management] (/admin/exercises)
 │    ├── L&R Exercise Bank (Part 1 - 7) (/admin/exercises/lr)
 │    ├── Speaking Exercise Bank (Part 1 - 5) (/admin/exercises/speaking)
 │    └── Writing Exercise Bank (Part 1 - 3) (/admin/exercises/writing)
 ├── [Skill-Based Exam Management] (/admin/exams)
 │    ├── L&R Exam Management (200 questions, Master Audio, 990 Scale) (/admin/exams/lr)
 │    │    ├── Create New / Fork Revision (/admin/exams/lr/create)
 │    │    ├── Author 200 Slots (/admin/exams/lr/[examId]/edit)
 │    │    └── Import Exam from Excel XLSX (/admin/exams/lr/import)
 │    ├── Speaking Exam Management (11 questions, Prep/Resp Time, 200 Rubric) (/admin/exams/speaking)
 │    └── Writing Exam Management (8 questions, Target words, 200 Rubric) (/admin/exams/writing)
 ├── [Media Library Management] (/admin/media)
 │    ├── Image Repository (/admin/media/images)
 │    └── Audio Repository (/admin/media/audio)
 └── [ETS Score Profile Management] (/admin/score-profiles)
```

---

# 4. LEARNER APP FUNCTIONAL SPECIFICATIONS

## 4.1. Authentication, Guest & Profile Flows

### 4.1.1. Guest Mode Experience
- **UX Goal:** Provide an opportunity to try test-taking features directly without upfront registration barriers.
- **Business Rules:**
  - Guests can select and try a standalone L&R exam, standalone Speaking exam, standalone Writing exam, or any Part exercise.
  - At the top of the Guest's test screen, there is always a **Sticky Alert Banner (Soft Amber/Yellow)**:
    > *"You are taking this test as a Guest. Your test results will not be saved to personal history after closing the browser. [Register for free] to save your study roadmap."*
  - When a Guest clicks "Submit", the system still calculates and displays the score for that test.
    However, clicking *"Save result"* or *"View 100% detailed solutions"* opens a Modal prompting account registration (1-click Google or Email).
  - No shadow account is created; uses a temporary session identifier (`X-Anonymous-Session`) stored in Cookie/Local Storage.

### 4.1.2. Email + OTP Registration/Login & Google Login
- **Email Registration:** Enter Display Name (1-100 characters), valid Email, Password (minimum 8 characters, with strength meter).
  The Backend sends a 6-digit OTP to the email, valid for 10 minutes (maximum 5 wrong attempts, 60s resend cooldown).
- **Google Login:** *"Continue with Google"* button.
  Handles the event when the user closes the popup without reloading the page, preserving form state and showing a subtle notice *"Google login cancelled"*.
  Automatically creates an account with the `Learner` role on the Google account's first visit.
- **Personal Profile:** Displays avatar (supports JPG/PNG/WebP upload <= 2MB with circular crop), Display Name (editable), Email (read-only with lock icon), desired Target Score, and Change Password.

---

## 4.2. Skill & Part-based Practice Mode Subsystem

Part-based Practice Mode serves **deep learning and reinforcing knowledge per question type**, completely separated from mock exams.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                 DISTRIBUTION TABLE OF 15 INDEPENDENT PRACTICE PARTS         │
├──────────────────────────┬──────────────────────────────────────────────────┤
│ SKILL                    │ INDIVIDUAL PRACTICE PARTS                        │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 1. TOEIC Listening       │ Part 1: Photographs (4 options)                  │
│                          │ Part 2: Question-Response (3 options)            │
│                          │ Part 3: Conversations (3-question sets)          │
│                          │ Part 4: Talks (3-question sets)                  │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 2. TOEIC Reading         │ Part 5: Incomplete Sentences                     │
│                          │ Part 6: Text Completion                          │
│                          │ Part 7: Reading Comprehension                    │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 3. TOEIC Speaking        │ Part 1: Read a text aloud                        │
│                          │ Part 2: Describe a picture                       │
│                          │ Part 3: Respond to questions                     │
│                          │ Part 4: Respond using provided info (Docs)       │
│                          │ Part 5: Express an opinion                       │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 4. TOEIC Writing         │ Part 1: Write a sentence based on picture & 2 kw │
│                          │ Part 2: Respond to a written request (Email)     │
│                          │ Part 3: Write an opinion essay (Min 300 words)   │
└──────────────────────────┴──────────────────────────────────────────────────┘
```

- **Practice Mode UI Characteristics:**
  - No strict time limits; learners control their own pace.
  - **Instant Feedback:** Upon selecting an option, the system immediately indicates Correct (Green) / Incorrect (Red with the correct answer).
  - **Full-Control Audio Player:** Seek forward/backward 5 seconds, A-B loop playback, adjust playback speed (0.8x, 1.0x, 1.2x).
  - **Smart Explanation Drawer:** Expands directly below the question with 3 tabs: Detailed Explanation, Bilingual English-Vietnamese Transcript with corresponding sentence replay, and Key Vocabulary.
  - **Speaking Practice:** Test recording -> Replay your own voice -> Compare with Native Speaker Sample Audio and sample transcript.
  - **Writing Practice:** Text editor with real-time word counter, validation for mandatory keywords (Part 1), comparison against top-scoring model essays and grading criteria.

---

## 4.3. ETS-Standard Independent Mock Test Engines Subsystem

### 4.3.0. Independent Mock Test Organization Principles & Test-Taking Lifecycle
When entering the Exam Library (`/exams`), learners see **3 separate exam sections**.
Learners click to select the exam type they want to take:

```
                  ┌───────────────────────────────┐
                  │     EXAM LIBRARY (/exams)     │
                  └───────────────┬───────────────┘
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ [1] L&R EXAM     │    │[2] SPEAKING EXAM │    │ [3] WRITING EXAM │
│ - 200 questions  │    │ - 11 questions   │    │ - 8 questions    │
│ - 120 minutes    │    │ - ~20 minutes    │    │ - 60 minutes     │
│ - ETS 990 Scale  │    │ - ETS 200 Scale  │    │ - ETS 200 Scale  │
└─────────┬────────┘    └─────────┬────────┘    └─────────┬────────┘
          │                       │                       │
     [Take L&R]            [Take Speaking]          [Take Writing]
          │                       │                       │
          ▼                       ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   L&R RESULTS    │    │ SPEAKING RESULTS │    │ WRITING RESULTS  │
│   (990 Scale)    │    │   (200 Scale)    │    │   (200 Scale)    │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

Each exam is an independent session (separate `Attempt`):
- No prerequisite requiring completion of one exam before taking another.
- Independent countdown timer and question set.
- Independent submission and scoring according to the standard Score Profile of that exam.

---

### 4.3.1. Independent TOEIC Listening & Reading Test (200 questions - 120 minutes)

#### A. Waiting & Pre-exam Instructions Screen
- Exam title: e.g., *"ETS 2026 Test 1 - Listening & Reading"*.
- Format: Exactly 200 questions divided into 2 sections: Listening (Questions 1-100) and Reading (Questions 101-200).
- Total duration: 120 continuous minutes.
- Rules:
  - The Listening Master Audio plays continuously from start to finish; seeking and pausing are disabled.
  - When the Listening audio stream ends or moves to the next question, previous questions are locked to prevent re-listening or changing answers.
  - After 120 minutes, the system automatically collects and submits the exam.
- Primary button: **[START L&R EXAM]**.

#### B. Virtual L&R Exam Room Layout
- **Zone 1 (Fixed Topbar):**
  - Exam title, *"Listening & Reading"* badge.
  - Server-synced countdown timer formatted as `01:59:45`.
  - **[Submit L&R]** button.
- **Zone 2 (Test Split-View):**
  - Left column (55%): Master Audio Player (continuous playback), illustrations (Part 1, 3, 4), or reading passages (Part 6, 7).
    Supports independent scrolling and a Highlighter tool.
  - Right column (45%): Question and answer options A, B, C, D (or A, B, C in Part 2).
    Includes a Flag button and navigation buttons.
- **Zone 3 (200-Question Matrix - Question Palette):**
  - Displays the full 200-cell square grid.
  - Intuitive color coding: White (unanswered), Green (answered), Orange (current question, synchronously highlights the entire passage group when clicking a child question), Yellow flag (flagged).
  - Quick filter tabs: *All (200)*, *Listening (1-100)*, *Reading (101-200)*.

#### C. Strict Technical Rules for the L&R Exam Room
1. **Server-synced Countdown:** End time = `StartTimeUtc + 120 minutes`.
   Automatically syncs with the Server even if the learner changes their computer clock or refreshes (F5) the page.
   `<= 5 minutes` turns orange; `<= 1 minute` turns blinking red; `00:00:00` auto-submits.
2. **Disable Audio Seeking:** Master Audio plays continuously, hiding the seekbar and pause button.
   Permanently locks the 100 Listening questions once moved to the Reading section (from question 101 onward).
3. **Instant Autosave:** Saves to Local Storage and dispatches background updates to the Server after each answer selection (500ms debounce).
   Network drops or closing and reopening the tab restores 100% of test progress.
4. **L&R Submission Confirmation Modal:** Summary: *"Answered X/200 questions, Y unanswered. Are you sure you want to submit the L&R exam?"*.
5. **Anti-cheating:** Disables right-click, text copying, and F12 inspect; exam payload returned by the API strictly excludes `correctKey`, `transcript`, and `explanation`.

---

### 4.3.2. Independent TOEIC Speaking Test (11 questions - ~20 minutes)

#### A. Mandatory Device Check
Learners choosing the Speaking exam must complete 3 check steps before entering the exam:
1. Grant Microphone permission to the browser.
2. Speak a test sentence to verify the green mic input volume meter.
3. Listen to a sample audio clip to confirm speakers/headphones work properly.
- Only after completing all 3 steps does the **[ENTER SPEAKING EXAM]** button unlock.

#### B. Automatic 2-Phase Transition Mechanism for 11 Speaking Questions
Each Speaking question operates automatically following the strict ETS cycle:
- **Phase 1 (Preparation):** Screen displays the prompt (text passage, image, schedule, or question).
  Countdown timer runs for preparation time (e.g., 45s).
  Microphone is OFF.
- **Transition:** Preparation timer hits 0, speakers play the ETS-standard "Beep" tone.
- **Phase 2 (Response):** Response countdown timer starts running.
  Microphone turns ON AUTOMATICALLY, red REC icon blinks, displaying a real-time voice-reactive Live Audio Waveform.
- **Question End:** Timer hits 0, system immediately stops recording.
  **Re-recording is strictly prohibited**.
  System saves the audio file in at most 2 seconds and automatically advances to the next question.

#### C. Timing Configuration Table for 11 Speaking Questions

| Question | Part | Question Type | Preparation Time | Response Time |
| :---: | :---: | :--- | :---: | :---: |
| **Q1 - 2** | Part 1 | Read a text aloud | 45 seconds | 45 seconds |
| **Q3 - 4** | Part 2 | Describe a picture | 45 seconds | 30 seconds |
| **Q5 - 6** | Part 3 | Respond to questions (Quick Q&A) | 3 seconds | 15 seconds |
| **Q7** | Part 3 | Respond to questions (Detailed Q&A) | 3 seconds | 30 seconds |
| **Q8 - 9** | Part 4 | Respond to questions using info | Read doc: 45s / Per Q: 3s | 15 seconds |
| **Q10** | Part 4 | Respond to questions using info | 3 seconds | 30 seconds |
| **Q11** | Part 5 | Express an opinion | 45 seconds | 60 seconds |

---

### 4.3.3. Independent TOEIC Writing Test (8 questions - 60 minutes)

#### A. Writing Exam Room Layout
- **Fixed Header:** Exam title, *"Writing"* badge, total 60-minute countdown timer (or per-section timer), **[Submit Writing]** button.
- **2-Pane Layout:**
  - Prompt Pane:
    - *Q1 - 5 (Part 1):* Displays the picture and 2 mandatory keywords below the image.
    - *Q6 - 7 (Part 2):* Displays the incoming email along with 2-3 mandatory response directions.
    - *Q8 (Part 3):* Displays the opinion essay prompt.
  - Text Composition Pane:
    - Dedicated `<textarea>` input.
    - Real-time Word Count Indicator.
    - Automatic keyword indicator turning green when each keyword appears (for Part 1).

#### B. Strict Technical Constraints for the Writing Exam Room
1. **Disable Spellcheck:** `spellcheck="false"`, blocking browser extensions such as Grammarly or LanguageTool from interfering.
2. **Block External Paste:** `onPaste` event blocks all clipboard data copied from outside the browser.
   Candidates can only type directly or copy/paste text they typed inside the exam editor itself.
3. **Text Autosave:** Automatically saves text to the Server after 800ms of typing inactivity.
   Ensures 100% intact recovery upon network failure or page reload.

---

## 4.4. Separate Results & Review Modes Subsystem

> [!IMPORTANT]
> Because learners take independent exams, the system provides **3 separate Result Report screens and 3 separate Review Modes**, optimized for the characteristics of each exam type.

### 4.4.1. Result Report & Review Mode for L&R Test (ETS 990 Scale)
- **L&R Result Report Screen:**
  - **Overall Score:** Displays ETS-standard converted score **10 - 990 points** (Large, prominent typography).
  - **Component Scores:** Listening Score: **... / 495** | Reading Score: **... / 495**.
  - **Correct Answer Counts:** Listening: X/100 questions | Reading: Y/100 questions.
  - **7-Part Proficiency Chart:** Horizontal bar chart showing accuracy percentage from Part 1 to Part 7, highlighting strong Parts and weak Parts needing practice.
- **Detailed L&R Review Mode:**
  - Reopens all 200 questions in Split-View (Read-only).
  - Color-coded 200-question matrix: Green (Correct), Red (Incorrect), Gray (Unanswered).
    Quick filters: *"Incorrect only"*, *"Flagged questions"*.
  - For each question: Clearly displays the learner's selected option (red border if wrong) and the ETS-standard correct answer (dark green border).
  - Study Drawer: Audio player to replay that question's audio segment, bilingual English-Vietnamese **Interactive Transcript** (highlighting sentences containing evidence for the answer), full passage translation, and detailed grammar/vocabulary explanation.

### 4.4.2. Result Report & Review Mode for Speaking Test (ETS 200 Scale)
- **Speaking Result Report Screen:**
  - **Overall Score:** Estimated converted score **0 - 200 points** (in 10-point increments).
  - Evaluation status: *"Evaluation completed"* (or *"In evaluation queue..."* if pending processing).
  - Evaluated across 5 ETS-standard criteria: Pronunciation, Intonation & Stress, Grammar, Vocabulary, Cohesion & Relevance.
- **Detailed Speaking Review Mode:**
  - List of 11 Speaking questions grouped by 5 Parts.
  - For each question:
    - Audio Player replaying **the exact voice recording submitted by the learner** during the test.
    - Audio Player playing the **Native Speaker Sample Audio**.
    - Top-scoring sample transcript with intonation and stress analysis.

### 4.4.3. Result Report & Review Mode for Writing Test (ETS 200 Scale)
- **Writing Result Report Screen:**
  - **Overall Score:** Estimated converted score **0 - 200 points** (in 10-point increments).
  - Detailed 3-part breakdown:
    - Part 1 (Q1-5): Evaluates grammar, picture relevance, and proper usage of the 2 mandatory keywords.
    - Part 2 (Q6-7): Evaluates quality and completeness of the response email against the directions.
    - Part 3 (Q8): Evaluates the opinion essay (intro-body-conclusion structure, arguments, idea development, academic vocabulary).
- **Detailed Writing Review Mode:**
  - Displays verbatim the entire text the learner typed during the exam.
  - Highlights grammar/spelling errors to fix.
  - Provides an ETS-standard **High-score Model Essay** for learners to compare and learn phrasing.

---

## 4.5. Attempt History & Skill-based Progress Tracking Subsystem
- The Attempt History screen (`/history`) is divided into **3 independent Tabs**:
  - **Tab 1: Listening & Reading Exam History:** List of L&R attempts, 990 score, L/R subscores, test date, button to reopen L&R Review Mode.
  - **Tab 2: Speaking Exam History:** List of Speaking attempts, 200 score, test date, button to reopen Speaking Review Mode.
  - **Tab 3: Writing Exam History:** List of Writing attempts, 200 score, test date, button to reopen Writing Review Mode.
- Progression Chart: Learners can view separate score progression trend charts over time for each skill.

---

# 5. ADMIN PORTAL FUNCTIONAL SPECIFICATIONS

## 5.1. Admin Overview Dashboard
- Route: `/admin/overview`
- **4 KPI Cards:**
  - Total learners on the system & weekly growth.
  - Total exam submissions (clearly separated: L&R, Speaking, Writing submissions).
  - Ready exam bank (`PUBLISHED`): Counts of L&R exams, Speaking exams, and Writing exams.
  - AI Writing/Speaking evaluation queue status.
- **Action Required Alert Center:** Alerts for Draft exams missing audio/score profiles, and submissions with evaluation errors.

---

## 5.2. User Management & Permissions
- Route: `/admin/users`
- Paginated account list (20 users/page), search by Name/Email, filter by Status (`Active`, `Pending`, `Locked`), filter by Role (`Learner`, `Admin`).
- **Lock Account Action:** Opens a modal requiring a lock reason.
  Immediately revokes active sessions and logs the account out instantly.
- **Unlock Account Action:** Restores active status.
- **View User Details:** View profile info and lists of L&R, Speaking, and Writing exams taken by this user.

---

## 5.3. Part-based Exercise & Standalone Question Bank
- Route: `/admin/exercises`
- Manages the independent question bank used for Part-based Practice Mode (completely separated from mock exam question banks).
- Divided into 3 groups: L&R (Part 1-7), Speaking (Part 1-5), Writing (Part 1-3).
- Supports standalone questions (Part 1, 2, 5) and question clusters sharing Text/Audio/Image context (Part 3, 4, 6, 7).
- Mandatory Part tagging, Difficulty level (Easy/Medium/Hard), Topic.
- Input fields for Transcript, Vietnamese Translation, Detailed Explanation, Sample Audio, and Model Essay.
- Excel import tool for Part exercises with row-by-row preview and error validation.

---

## 5.4. TOEIC Listening & Reading Exam Management
- Route: `/admin/exams/lr`
- **Immutable Versioned Revisions:** Published exams (`PUBLISHED`) are immutable to protect learner historical data.
  To make edits, Admin clicks **[Fork New Draft]** to create a new draft revision.
- **ETS-Standard Fixed 200-Slot Frame:** Automatically generates a 200-question frame following the standard group structure (Part 1: 6 questions, Part 2: 25 questions, Part 3: 39 questions, Part 4: 30 questions, Part 5: 30 questions, Part 6: 16 questions, Part 7: 54 questions).
- **Master Audio Management:** L&R exams require uploading a single combined master audio file (.mp3 or .wav <= 100MB).
  Uploading separate audio files on individual child questions is disallowed.
- **Publish Checklist:** Blocks publishing if fewer than 200/200 questions exist, any question lacks an answer key, Master Audio is missing, or no Score Profile (990 scale) is linked.
- **Import L&R Exam via 4-sheet Excel XLSX:** `Manifest`, `Blocks`, `ContextBlocks`, `Questions` accompanied by the Master Audio file.

---

## 5.5. TOEIC Speaking & Writing Exam Management

### 5.5.1. Speaking Exam Management (/admin/exams/speaking)
- Author independent Speaking exams containing exactly 11 questions across 5 Parts.
- Configure per question: Preparation time (seconds) and Response time (seconds).
- Upload question prompt audio files, picture illustrations (Part 2), and document tables (Part 4).
- Upload Native Speaker Sample Audio and top-scoring sample Transcript.
- Publish constraints: All 11/11 questions present, 100% configured with standard timings and sample responses.

### 5.5.2. Writing Exam Management (/admin/exams/writing)
- Author independent Writing exams containing exactly 8 questions across 3 Parts:
  - Part 1 (Q1-5): Upload 1 picture + Enter exact 2 mandatory keywords.
  - Part 2 (Q6-7): Enter incoming email + List of response directions.
  - Part 3 (Q8): Enter essay prompt + Configure minimum recommended word count.
- Enter Model Essay and Grading Rubric for each question.
- Publish constraints: All 8/8 questions present with complete keywords, prompts, and model essays.

---

## 5.6. Media Asset Management
- Stored on Object Storage (MinIO S3).
- Images: PNG, JPG, JPEG, WebP `<= 5MB`.
  Validates file signature and returns an immutable Asset UUID.
- Audio: MP3, WAV `<= 15MB` (for standalone exercises) or `<= 100MB` (for L&R exam Master Audio).
- Lightbox preview for images and waveform audio player for audio assets.

---

## 5.7. ETS-Standard Score Profiles Management
- Route: `/admin/score-profiles`
- ETS 990 standard conversion tables for Listening & Reading exams:
  - 101-entry Listening conversion table (0-100 correct answers -> 5-495 points).
  - 101-entry Reading conversion table (0-100 correct answers -> 5-495 points).
- Admin can create multiple Score Profiles and assign them to corresponding L&R exams.

---

# 6. UX/UI DESIGN DIRECTIVES FOR DESIGNERS

### 6.1. Core EdTech User Experience Principles
1. **Clarity & Exam Separation:** The Exam Library UI (`/exams`) must clearly present 3 independent card choices: *Listening & Reading Exam*, *Speaking Exam*, *Writing Exam*.
   Avoid any misconception that learners must take all 4 skills in one sitting.
2. **Deep Focus inside the Exam Room:** When entering any exam room (L&R, Speaking, Writing), completely hide the website's main Header/Footer and retain only the exam Topbar (Exam title, skill badge, countdown timer, submit button).
3. **Clear & Empathetic Feedback:**
   - Practice: Positive emotional feedback when right, supportive explanation when wrong.
   - Result Reports: Clear color coding, specific strengths/weaknesses analysis for the skill the learner just completed.

### 6.2. Design of Specific UI Components
1. **Exam Category Cards (/exams Page):**
   - 3 prominent large Cards representing the 3 exams:
     - L&R Card: Headphones & book icon, label *"200 questions | 120 mins | 990 Scale"*.
     - Speaking Card: Microphone icon, label *"11 questions | 20 mins | 200 Scale"*.
     - Writing Card: Pen icon, label *"8 questions | 60 mins | 200 Scale"*.
2. **L&R Question Matrix (Question Palette - 200 cells):** `36x36px` cells, `6px` border radius.
   White (unanswered), Green (answered), Orange (selected / highlights entire child question cluster), Yellow flag (flagged).
3. **Countdown Timer HUD:** Monospace font (`Roboto Mono`).
   `>5` minutes (black/gray text), `<=5` minutes (yellow background, orange text), `<=1` minute (red background, blinking red text).
4. **Split-View Layout:** Applied to Part 6, 7 (L&R) and Part 2, 3 (Writing).
   Passage/context column (55%) and Question/editor column (45%) scroll independently.
5. **Speaking Recording Frame (Waveform Visualizer):** Circular indicator shifting from Gray (Preparation Phase) to Red REC (Response Phase) accompanied by a real-time voice-reactive audio waveform bar.

### 6.3. UI States Specification
Every screen and Component must be designed with all 5 states:
1. **Normal State:** Complete data, balanced layout.
2. **Hover / Focus / Active State:** Smooth transition animations (<200ms).
3. **Loading State:** Use **Skeleton UI (pulsing gray skeleton frames)** instead of a plain loading spinner.
4. **Empty State:** Friendly illustration with a call-to-action (CTA) button: *"No exams in your history yet. [Explore exams now]"*.
5. **Error State:** Banner notifying of network issues or system errors, assuring that test progress data has been safely saved.

### 6.4. Responsive & Breakpoints Guidelines
- **L&R, Speaking, Writing Mock Exam Rooms:** Optimized **Desktop-First (>= 1024px)** to guarantee adequate space for Split-View, Question Palette, and realistic exam-room mic controls.
- **Part-based Practice Mode:** Optimized **Mobile-First & Fully Responsive** (Mobile 375-430px, Tablet 768-1024px, Desktop >1024px).
  On mobile, Split-View switches to 2 swipeable Tabs (Prompt Tab and Question Tab).

---

# 7. NON-FUNCTIONAL REQUIREMENTS & SYSTEM SAFETY

1. **Page Load Performance:** Exam loading `< 1.5` seconds.
   Master Audio plays via Audio Streaming, without forcing a full 100MB download before starting the test.
2. **Fault Tolerance & Recovery:** 100% restoration of answer state and Server-synced timer upon F5 refresh, network loss, or closing the browser.
   Server prevents 1 account from opening 2 concurrent tabs for the same exam session.
3. **Exam Security & Anti-Cheating (Exam Integrity):** Frontend must not contain `correctKey`, `transcript`, or `explanation` during an active exam.
   Blocks external copy-paste in Writing tests and blocks F12/Inspect.
4. **Account Security:** Passwords hashed with BCrypt + Salt, session managed via `HttpOnly Secure Cookie`.

---

# 8. OVERALL ACCEPTANCE CRITERIA MATRIX

| AC Code | Category | Specific Acceptance Criteria |
| :--- | :--- | :--- |
| **AC-HUB-01** | Exam Selection | Learners can select standalone L&R, Speaking, or Writing exams from `/exams`; each exam initializes an independent session (`Attempt`). |
| **AC-LR-01** | L&R Mock Test | Exactly 200 ETS-standard questions; continuous Master Audio without seek/pause; 120-minute Server-synced timer; auto-submit on timeout. |
| **AC-LR-02** | L&R Scoring | Automatically maps against ETS 990 Score Profile; displays Listening score (5-495), Reading score (5-495), total score (10-990), and % accuracy per Part 1-7. |
| **AC-LR-03** | L&R Review | Review mode displays all 200 questions; learner answer vs. correct answer; bilingual Interactive Transcript; detailed explanations. |
| **AC-SPEAK-01**| Speaking Mock Test| Mandatory Mic check before test; auto-transitions Preparation -> Beep -> Response; auto-records and cuts off at timeout; prohibits re-recording. |
| **AC-SPEAK-02**| Speaking Results| Returns converted score on 200 scale; replays exact candidate voice recording; provides native speaker sample audio and sample transcript. |
| **AC-WRITE-01**| Writing Mock Test| Disables spellcheck and blocks external paste; real-time word count; periodic 800ms autosave; independent 60-minute timer. |
| **AC-WRITE-02**| Writing Results| Returns converted score on 200 scale; displays submitted text; provides ETS-standard model essays and rubric feedback. |
| **AC-PRAC-01** | Practice Mode | Covers all 15 Parts of 4 skills; instant right/wrong feedback; A-B loop audio; displays detailed explanation directly below the question. |
| **AC-HIST-01** | Attempt History | History page divided into 3 independent tabs: L&R Tab, Speaking Tab, Writing Tab; allows reopening corresponding results and review modes. |
| **AC-ADM-01** | User Admin | Paginated list, search/filter; Lock account with mandatory reason and immediate session revocation; successful account unlock. |
| **AC-ADM-02** | L&R Authoring | Fixed 200-slot frame; immutable Draft/Published revision mechanism; blocks publishing if under 200 questions, missing Master Audio, or missing Score Profile. |
| **AC-ADM-03** | S&W Authoring | Separately manages Speaking exams (11 questions) and Writing exams (8 questions); enforces complete question count, standard timings, and sample responses before publishing. |
| **AC-ADM-04** | Excel Import | Supports importing L&R exams via 4-sheet XLSX + Master Audio; validates data and highlights detailed errors if any issues exist. |

---

> **CLOSING NOTE FOR THE PROJECT TEAM:**  
> Requirement Specification Version **2.1** has accurately standardized the business model: **Learners choose to take and receive independent results for each exam (Listening & Reading, Speaking, Writing)**.  
> - **UI/UX Designer:** Design the Exam Library page (`/exams`) with 3 clear card sections, build 3 exam room flows and 3 separate result/review mode screens following Sections 3, 4, and 6.  
> - **Developers (FE/BE):** Implement Entity architecture, API endpoints, Attempt sessions, and Score Profiles independently for each exam type according to the acceptance criteria in Section 8.
