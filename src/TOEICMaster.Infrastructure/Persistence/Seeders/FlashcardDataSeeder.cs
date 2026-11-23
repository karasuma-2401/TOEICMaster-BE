using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Seeders;

public static class FlashcardDataSeeder
{
    private const long FlashcardSeedLockKey = 0x544F454943464353;

    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task SeedBuiltInDecksAsync(
        ToeicDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async retryToken =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(retryToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({FlashcardSeedLockKey})", retryToken);
            try
            {
                await SeedBuiltInDecksCoreAsync(dbContext, retryToken);
                await transaction.CommitAsync(retryToken);
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }

    private static async Task SeedBuiltInDecksCoreAsync(
        ToeicDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingSlugs = await dbContext.FlashcardDecks
            .AsNoTracking()
            .Where(deck => deck.OwnerUserId == null)
            .Select(deck => deck.Slug)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var builtIns = BuildDefaultDecks()
            .Where(deck => !existingSlugs.Contains(deck.Slug))
            .ToArray();

        if (builtIns.Length == 0)
            return;

        dbContext.FlashcardDecks.AddRange(builtIns);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static IReadOnlyList<FlashcardDeck> BuildDefaultDecks() =>
    [
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            "office-operations",
            "Office & Business Operations",
            "Core vocabulary for meeting schedules, internal memos, equipment, and workflows commonly seen in Parts 3, 4, and 7.",
            FlashcardCategories.Office,
            "500–750+",
            8,
            [
                (Guid.Parse("10000001-0000-4000-8000-000000000001"), new("itinerary", "/aɪˈtɪn.ə.rer.i/", "noun", "lịch trình chi tiết (chuyến đi, công tác)", "Please review the updated travel itinerary before Monday's flight.", "Vui lòng xem lại lịch trình công tác đã cập nhật trước chuyến bay thứ Hai.", ["travel itinerary", "detailed itinerary", "revise an itinerary"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000002"), new("accommodate", "/əˈkɑː.mə.deɪt/", "verb", "đáp ứng (nhu cầu), chứa được (số lượng người)", "The conference hall can accommodate up to 300 attendees comfortably.", "Hội trường có thể chứa thoải mái tới 300 người tham dự.", ["accommodate a request", "accommodate guests", "accommodate changes"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000003"), new("agenda", "/əˈdʒen.də/", "noun", "chương trình nghị sự, danh sách nội dung cuộc họp", "The budget review is the first item on today's meeting agenda.", "Rà soát ngân sách là mục đầu tiên trong chương trình họp hôm nay.", ["on the agenda", "set the agenda", "meeting agenda"], "Part 3 · Part 4", "B1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000004"), new("mandatory", "/ˈmæn.də.tɔːr.i/", "adjective", "bắt buộc (theo quy định)", "Attendance at the workplace safety briefing is mandatory for all staff.", "Việc tham dự buổi phổ biến an toàn lao động là bắt buộc đối với toàn bộ nhân viên.", ["mandatory training", "mandatory requirement", "make it mandatory"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000005"), new("streamline", "/ˈstriːm.laɪn/", "verb", "tinh giản, tối ưu hóa quy trình", "The new software will streamline our invoice approval workflow.", "Phần mềm mới sẽ tối ưu hóa quy trình phê duyệt hóa đơn của chúng ta.", ["streamline operations", "streamline a process", "streamline production"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000006"), new("correspondence", "/ˌkɔːr.əˈspɑːn.dəns/", "noun", "thư từ giao dịch, trao đổi bằng văn bản", "All client correspondence must be archived in the shared folder.", "Mọi thư từ trao đổi với khách hàng phải được lưu trữ trong thư mục dùng chung.", ["business correspondence", "official correspondence", "handle correspondence"], "Part 6 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000007"), new("designate", "/ˈdez.ɪɡ.neɪt/", "verb", "chỉ định, phân công chính thức", "Management designated the second floor as a quiet working zone.", "Ban quản lý đã chỉ định tầng hai làm khu vực làm việc yên tĩnh.", ["designated area", "designate a representative", "designated parking"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000008"), new("promptly", "/ˈprɑːmpt.li/", "adverb", "đúng giờ, nhanh chóng, kịp thời", "The quarterly orientation session will begin promptly at 9:00 A.M.", "Buổi định hướng hàng quý sẽ bắt đầu đúng 9:00 sáng.", ["respond promptly", "arrive promptly", "begin promptly at"], "Part 4 · Part 5", "B2")),
            ],
            SeedTimestamp),
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000002"),
            "contracts-negotiation",
            "Contracts & Commercial Negotiation",
            "Master terms on clauses, renewals, compliance, and agreements that appear frequently in Part 6-7 emails and notices.",
            FlashcardCategories.Contracts,
            "650–850+",
            9,
            [
                (Guid.Parse("10000002-0000-4000-8000-000000000001"), new("stipulate", "/ˈstɪp.jə.leɪt/", "verb", "quy định rõ trong hợp đồng hoặc thỏa thuận", "The lease stipulates that tenants must give 30 days' notice before moving out.", "Hợp đồng thuê quy định rõ người thuê phải báo trước 30 ngày trước khi chuyển đi.", ["stipulate conditions", "as stipulated in", "contract stipulates"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000002"), new("abide by", "/əˈbaɪd baɪ/", "phrasal verb", "tuân thủ, chấp hành (luật lệ, thỏa thuận)", "Both parties agreed to abide by the revised confidentiality clause.", "Cả hai bên đã đồng ý tuân thủ điều khoản bảo mật sửa đổi.", ["abide by the terms", "abide by regulations", "abide by an agreement"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000002-0000-4000-8000-000000000003"), new("provision", "/prəˈvɪʒ.ən/", "noun", "điều khoản; sự cung cấp", "We added a warranty provision covering replacement parts for two years.", "Chúng tôi đã bổ sung điều khoản bảo hành bao gồm linh kiện thay thế trong hai năm.", ["contract provision", "under the provisions of", "make provision for"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000004"), new("renew", "/rɪˈnuː/", "verb", "gia hạn (hợp đồng, gói đăng ký, giấy phép)", "Clients who renew their annual subscription early receive a 15% discount.", "Khách hàng gia hạn gói thường niên sớm sẽ nhận được ưu đãi giảm 15%.", ["renew a contract", "automatic renewal", "renew a membership"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000005"), new("obligate", "/ˈɑːb.lɪ.ɡeɪt/", "verb", "ràng buộc, bắt buộc theo nghĩa vụ", "The supplier is obligated to deliver all components by Friday.", "Nhà cung cấp có nghĩa vụ giao toàn bộ linh kiện trước thứ Sáu.", ["be obligated to", "contractual obligation", "meet an obligation"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000002-0000-4000-8000-000000000006"), new("amendment", "/əˈmend.mənt/", "noun", "bản sửa đổi, phụ lục điều chỉnh", "Any amendment to the service agreement requires written approval.", "Mọi sửa đổi đối với thỏa thuận dịch vụ đều cần phê duyệt bằng văn bản.", ["proposed amendment", "make an amendment", "contract amendment"], "Part 6 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000007"), new("consensus", "/kənˈsen.səs/", "noun", "sự đồng thuận, nhất trí chung", "After two hours of negotiation, the committee reached a consensus.", "Sau hai giờ đàm phán, ủy ban đã đạt được sự đồng thuận.", ["reach a consensus", "broad consensus", "build consensus"], "Part 3 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000008"), new("terminate", "/ˈtɜːr.mɪ.neɪt/", "verb", "chấm dứt, hủy bỏ (hợp đồng)", "Either company may terminate the partnership upon written notice.", "Một trong hai công ty có thể chấm dứt quan hệ hợp tác khi có thông báo bằng văn bản.", ["terminate a contract", "early termination", "termination fee"], "Part 5 · Part 7", "B2")),
            ],
            SeedTimestamp),
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000003"),
            "finance-accounting",
            "Finance, Invoices & Budgets",
            "Essential vocabulary for TOEIC passages on revenue reports, reimbursements, audits, and accounting records.",
            FlashcardCategories.Finance,
            "600–850+",
            8,
            [
                (Guid.Parse("10000003-0000-4000-8000-000000000001"), new("reimburse", "/ˌriː.ɪmˈbɜːrs/", "verb", "hoàn trả chi phí (công tác, mua sắm)", "Keep all hotel receipts so the accounting team can reimburse you.", "Hãy giữ lại toàn bộ hóa đơn khách sạn để bộ phận kế toán có thể hoàn tiền cho bạn.", ["reimburse expenses", "travel reimbursement", "reimbursement form"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000002"), new("discrepancy", "/dɪˈskrep.ən.si/", "noun", "sự chênh lệch, sai khác (giữa số liệu, hóa đơn)", "The auditor noticed a discrepancy between the invoice and the purchase order.", "Kiểm toán viên phát hiện sự sai khác giữa hóa đơn và đơn đặt hàng.", ["billing discrepancy", "resolve a discrepancy", "report a discrepancy"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000003"), new("allocate", "/ˈæl.ə.keɪt/", "verb", "phân bổ (ngân sách, nguồn lực)", "The board allocated additional funds to expand the R&D division.", "Hội đồng quản trị đã phân bổ thêm ngân sách để mở rộng khối R&D.", ["allocate funds", "budget allocation", "allocate resources"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000004"), new("revenue", "/ˈrev.ə.nuː/", "noun", "doanh thu", "Online subscriptions accounted for 40% of our annual revenue.", "Các gói đăng ký trực tuyến chiếm 40% tổng doanh thu thường niên của chúng tôi.", ["annual revenue", "generate revenue", "revenue growth"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000005"), new("overdue", "/ˌoʊ.vɚˈduː/", "adjective", "quá hạn (thanh toán, bàn giao)", "A late fee applies to any balance that is more than 15 days overdue.", "Phí trễ hạn áp dụng cho mọi khoản dư quá hạn thanh toán hơn 15 ngày.", ["overdue payment", "overdue invoice", "long overdue"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000006"), new("expenditure", "/ɪkˈspen.dɪ.tʃɚ/", "noun", "khoản chi tiêu, kinh phí chi ra", "We need to curb operating expenditure during the third quarter.", "Chúng ta cần kiềm chế chi phí vận hành trong quý ba.", ["capital expenditure", "reduce expenditure", "public expenditure"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000007"), new("forecast", "/ˈfɔːr.kæst/", "noun / verb", "dự báo, bản dự đoán tài chính", "Analysts forecast steady export demand through the end of the year.", "Các nhà phân tích dự báo nhu cầu xuất khẩu ổn định đến hết năm.", ["sales forecast", "financial forecast", "forecast growth"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000008"), new("deduct", "/dɪˈdʌkt/", "verb", "khấu trừ, trừ bớt vào tổng tiền", "The discount will be automatically deducted at checkout.", "Khoản giảm giá sẽ được khấu trừ tự động khi thanh toán.", ["deduct from", "tax deduction", "automatically deducted"], "Part 5 · Part 6", "B2")),
            ],
            SeedTimestamp),
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000004"),
            "travel-hospitality",
            "Travel, Airlines & Hospitality",
            "Master conversations about room reservations, flight changes, and conferences in Parts 2, 3, and 4.",
            FlashcardCategories.Travel,
            "450–700+",
            7,
            [
                (Guid.Parse("10000004-0000-4000-8000-000000000001"), new("complimentary", "/ˌkɑːm.pləˈmen.t̬ɚ.i/", "adjective", "miễn phí (dịch vụ tặng kèm cho khách)", "All hotel guests receive complimentary breakfast and airport shuttle service.", "Mọi khách lưu trú đều được tặng bữa sáng miễn phí và xe đưa đón sân bay.", ["complimentary breakfast", "complimentary upgrade", "complimentary ticket"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000002"), new("occupancy", "/ˈɑːk.jə.pən.si/", "noun", "tỷ lệ lấp đầy phòng, sức chứa tối đa", "Peak-season occupancy at the resort reached 95% last July.", "Tỷ lệ lấp đầy phòng mùa cao điểm tại khu nghỉ dưỡng đạt 95% vào tháng 7 năm ngoái.", ["occupancy rate", "maximum occupancy", "double occupancy"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000003"), new("depart", "/dɪˈpɑːrt/", "verb", "khởi hành, rời bến", "The express train to Kyoto departs from Platform 4 in ten minutes.", "Chuyến tàu tốc hành đi Kyoto khởi hành từ sân ga số 4 sau mười phút nữa.", ["departure gate", "depart on schedule", "departure lounge"], "Part 2 · Part 4", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000004"), new("amenity", "/əˈmen.ə.t̬i/", "noun", "tiện nghi, dịch vụ tiện ích", "Modern amenities include high-speed Wi-Fi and a 24-hour fitness center.", "Các tiện nghi hiện đại gồm Wi-Fi tốc độ cao và phòng tập mở cửa 24 giờ.", ["hotel amenities", "local amenities", "on-site amenities"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000005"), new("confirm", "/kənˈfɜːrm/", "verb", "xác nhận (đặt chỗ, lịch hẹn)", "Could you confirm my reservation for two nights starting October 12?", "Bạn có thể xác nhận đặt phòng hai đêm bắt đầu từ ngày 12 tháng 10 giúp tôi không?", ["confirm a reservation", "confirmation number", "written confirmation"], "Part 2 · Part 3", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000006"), new("layover", "/ˈleɪ.oʊ.vɚ/", "noun", "thời gian quá cảnh giữa hai chặng bay", "Because of the three-hour layover in Seoul, I had time to answer emails.", "Nhờ có ba tiếng quá cảnh ở Seoul, tôi đã có thời gian trả lời email.", ["short layover", "overnight layover", "long layover"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000007"), new("beverage", "/ˈbev.ɚ.ɪdʒ/", "noun", "đồ uống, thức uống", "Light snacks and beverages will be served in the lobby during intermission.", "Đồ ăn nhẹ và thức uống sẽ được phục vụ tại sảnh trong giờ giải lao.", ["food and beverage", "refreshing beverage", "complimentary beverages"], "Part 1 · Part 4", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000008"), new("adjacent", "/əˈdʒeɪ.sənt/", "adjective", "liền kề, ngay sát bên", "Parking is available in the garage adjacent to the convention center.", "Chỗ đỗ xe có sẵn tại nhà xe nằm ngay cạnh trung tâm hội nghị.", ["adjacent to", "adjacent building", "adjacent rooms"], "Part 1 · Part 5", "B2")),
            ],
            SeedTimestamp),
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000005"),
            "marketing-sales",
            "Marketing, Product Launches & Sales",
            "Vocabulary for advertising, promotional campaigns, market research, and customer care in TOEIC tests.",
            FlashcardCategories.Marketing,
            "550–800+",
            8,
            [
                (Guid.Parse("10000005-0000-4000-8000-000000000001"), new("merchandise", "/ˈmɜːr.tʃən.daɪs/", "noun", "hàng hóa trưng bày, sản phẩm bán lẻ", "Seasonal merchandise is marked down by 30% until Sunday.", "Hàng hóa theo mùa được giảm giá 30% cho đến hết Chủ nhật.", ["defective merchandise", "display merchandise", "promotional merchandise"], "Part 1 · Part 7", "B2")),
                (Guid.Parse("10000005-0000-4000-8000-000000000002"), new("demographics", "/ˌdem.əˈɡræf.ɪks/", "noun", "nhân khẩu học (nhóm tuổi, thu nhập khách hàng)", "The campaign was tailored to younger urban demographics.", "Chiến dịch được thiết kế riêng cho nhóm khách hàng trẻ ở khu vực đô thị.", ["target demographics", "customer demographics", "demographic data"], "Part 4 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000003"), new("incentive", "/ɪnˈsen.tɪv/", "noun", "ưu đãi khuyến khích, động lực thúc đẩy", "Free express shipping served as a strong incentive for repeat orders.", "Miễn phí giao hàng nhanh là động lực lớn thúc đẩy đơn hàng lặp lại.", ["financial incentive", "offer an incentive", "sales incentive"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000005-0000-4000-8000-000000000004"), new("launch", "/lɔːntʃ/", "verb / noun", "ra mắt, tung ra thị trường (sản phẩm, chiến dịch)", "The company plans to launch its eco-friendly packaging line in May.", "Công ty dự kiến ra mắt dòng bao bì thân thiện môi trường vào tháng Năm.", ["product launch", "launch a campaign", "launch event"], "Part 3 · Part 6", "B1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000005"), new("endorse", "/ɪnˈdɔːrs/", "verb", "chứng thực, đại diện quảng bá, ủng hộ chính thức", "Several professional athletes endorse our new running shoes.", "Nhiều vận động viên chuyên nghiệp đại diện quảng bá cho dòng giày chạy bộ mới của chúng tôi.", ["celebrity endorsement", "officially endorse", "endorse a product"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000006"), new("competitive", "/kəmˈpet̬.ə.t̬ɪv/", "adjective", "có tính cạnh tranh cao (giá cả, chất lượng)", "We maintain competitive pricing without compromising material quality.", "Chúng tôi duy trì mức giá cạnh tranh mà không đánh đổi chất lượng vật liệu.", ["competitive edge", "competitive pricing", "highly competitive"], "Part 5 · Part 6", "B1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000007"), new("outperform", "/ˌaʊt.pɚˈfɔːrm/", "verb", "vượt trội hơn, đạt hiệu quả cao hơn", "The updated model outperformed rival devices in battery life tests.", "Mẫu cải tiến vượt trội hơn các thiết bị đối thủ trong bài kiểm tra thời lượng pin.", ["outperform competitors", "outperform expectations", "outperform the market"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000008"), new("survey", "/ˈsɜːr.veɪ/", "noun / verb", "khảo sát ý kiến khách hàng", "Please take two minutes to complete our customer satisfaction survey.", "Vui lòng dành hai phút để hoàn thành khảo sát mức độ hài lòng của khách hàng.", ["conduct a survey", "survey results", "satisfaction survey"], "Part 4 · Part 7", "B1")),
            ],
            SeedTimestamp),
        FlashcardDeck.CreateBuiltIn(
            Guid.Parse("10000000-0000-4000-8000-000000000006"),
            "hr-recruitment",
            "HR, Recruitment & Training",
            "Vocabulary for job applications, performance reviews, promotions, and employee benefits at all TOEIC levels.",
            FlashcardCategories.Hr,
            "500–800+",
            8,
            [
                (Guid.Parse("10000006-0000-4000-8000-000000000001"), new("applicant", "/ˈæp.lə.kənt/", "noun", "ứng viên nộp hồ sơ", "Successful applicants will be contacted for a second-round interview.", "Các ứng viên đạt yêu cầu sẽ được liên hệ để phỏng vấn vòng hai.", ["job applicant", "qualified applicant", "screen applicants"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000002"), new("credentials", "/krɪˈden.ʃəlz/", "noun (plural)", "bằng cấp, chứng chỉ và năng lực chuyên môn", "The hiring panel verified all academic credentials prior to making an offer.", "Hội đồng tuyển dụng đã xác minh toàn bộ bằng cấp học thuật trước khi gửi thư mời.", ["verify credentials", "professional credentials", "strong credentials"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000003"), new("probationary", "/proʊˈbeɪ.ʃə.ner.i/", "adjective", "thuộc giai đoạn thử việc", "Health benefits begin immediately after the 60-day probationary period.", "Quyền lợi bảo hiểm sức khỏe bắt đầu ngay sau giai đoạn thử việc 60 ngày.", ["probationary period", "probationary employee", "complete probation"], "Part 6 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000004"), new("proficient", "/prəˈfɪʃ.ənt/", "adjective", "thành thạo, giỏi chuyên môn", "Candidates must be proficient in spreadsheet modeling and data visualization.", "Ứng viên phải thành thạo lập mô hình bảng tính và trực quan hóa dữ liệu.", ["proficient in", "highly proficient", "language proficiency"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000005"), new("appraisal", "/əˈpreɪ.zəl/", "noun", "đánh giá hiệu suất làm việc", "Annual performance appraisals are scheduled for the first week of December.", "Các buổi đánh giá hiệu suất thường niên được lên lịch vào tuần đầu tháng 12.", ["performance appraisal", "conduct an appraisal", "annual appraisal"], "Part 3 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000006"), new("vacancy", "/ˈveɪ.kən.si/", "noun", "vị trí tuyển dụng đang trống", "Internal staff are encouraged to apply for the senior analyst vacancy.", "Nhân sự nội bộ được khuyến khích ứng tuyển vào vị trí chuyên viên phân tích cấp cao đang trống.", ["fill a vacancy", "job vacancy", "current vacancies"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000007"), new("eligible", "/ˈel.ə.dʒə.bəl/", "adjective", "đủ điều kiện, đủ tiêu chuẩn hưởng quyền lợi", "Full-time employees are eligible for tuition reimbursement after one year.", "Nhân viên toàn thời gian đủ điều kiện được hỗ trợ học phí sau một năm làm việc.", ["be eligible for", "eligible candidates", "eligibility criteria"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000008"), new("mentor", "/ˈmen.tɔːr/", "noun / verb", "người cố vấn; hướng dẫn, kèm cặp", "Each new hire is paired with an experienced mentor during onboarding.", "Mỗi nhân viên mới được ghép cặp với một người cố vấn giàu kinh nghiệm trong quá trình hội nhập.", ["mentorship program", "assign a mentor", "mentor junior staff"], "Part 3 · Part 7", "B2")),
            ],
            SeedTimestamp),
    ];
}
