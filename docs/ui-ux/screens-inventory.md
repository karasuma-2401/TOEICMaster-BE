# Danh mục 44 Màn hình Figma (Screens Inventory)

> Trích xuất từ [specs/figma-screens-master.md](../specs/figma-screens-master.md)

# 2. BẢNG TỔNG HỢP 44 MÀN HÌNH HIỆN CÓ TRÊN FIGMA

Tất cả 44 màn hình đều được bố trí trực quan trên **Page 1 (Node ID: `0:1`)**, được định danh theo quy chuẩn `[Role] Name - /route`:

| STT | Tên Frame trên Figma | Node ID | Kích Thước (W x H) | Phân Hệ | Route Đường Dẫn Frontend |
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
