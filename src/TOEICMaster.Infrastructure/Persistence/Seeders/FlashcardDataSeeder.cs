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
                (Guid.Parse("10000001-0000-4000-8000-000000000001"), new("itinerary", "/aɪˈtɪn.ə.rer.i/", "noun", "detailed schedule (trip, business travel)", "Please review the updated travel itinerary before Monday's flight.", "Please review the updated travel itinerary before Monday's flight.", ["travel itinerary", "detailed itinerary", "revise an itinerary"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000002"), new("accommodate", "/əˈkɑː.mə.deɪt/", "verb", "meet (a need), have room for (people)", "The conference hall can accommodate up to 300 attendees comfortably.", "The conference hall can comfortably hold up to 300 attendees.", ["accommodate a request", "accommodate guests", "accommodate changes"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000003"), new("agenda", "/əˈdʒen.də/", "noun", "meeting agenda, list of items to discuss", "The budget review is the first item on today's meeting agenda.", "Reviewing the budget is the first item on today's meeting agenda.", ["on the agenda", "set the agenda", "meeting agenda"], "Part 3 · Part 4", "B1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000004"), new("mandatory", "/ˈmæn.də.tɔːr.i/", "adjective", "required (by rule or policy)", "Attendance at the workplace safety briefing is mandatory for all staff.", "Attending the workplace safety briefing is mandatory for all staff.", ["mandatory training", "mandatory requirement", "make it mandatory"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000005"), new("streamline", "/ˈstriːm.laɪn/", "verb", "simplify, optimize a workflow", "The new software will streamline our invoice approval workflow.", "The new software will optimize our invoice approval workflow.", ["streamline operations", "streamline a process", "streamline production"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000006"), new("correspondence", "/ˌkɔːr.əˈspɑːn.dəns/", "noun", "business letters, written exchanges", "All client correspondence must be archived in the shared folder.", "All written exchanges with clients must be archived in the shared folder.", ["business correspondence", "official correspondence", "handle correspondence"], "Part 6 · Part 7", "B2")),
                (Guid.Parse("10000001-0000-4000-8000-000000000007"), new("designate", "/ˈdez.ɪɡ.neɪt/", "verb", "officially appoint or assign", "Management designated the second floor as a quiet working zone.", "Management designated the second floor as a quiet working zone.", ["designated area", "designate a representative", "designated parking"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000001-0000-4000-8000-000000000008"), new("promptly", "/ˈprɑːmpt.li/", "adverb", "on time, quickly, without delay", "The quarterly orientation session will begin promptly at 9:00 A.M.", "The quarterly orientation session will start right at 9:00 A.M.", ["respond promptly", "arrive promptly", "begin promptly at"], "Part 4 · Part 5", "B2")),
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
                (Guid.Parse("10000002-0000-4000-8000-000000000001"), new("stipulate", "/ˈstɪp.jə.leɪt/", "verb", "specify clearly in a contract or agreement", "The lease stipulates that tenants must give 30 days' notice before moving out.", "The lease clearly states that tenants must give 30 days' notice before moving out.", ["stipulate conditions", "as stipulated in", "contract stipulates"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000002"), new("abide by", "/əˈbaɪd baɪ/", "phrasal verb", "comply with, follow (rules, agreements)", "Both parties agreed to abide by the revised confidentiality clause.", "Both parties agreed to follow the revised confidentiality clause.", ["abide by the terms", "abide by regulations", "abide by an agreement"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000002-0000-4000-8000-000000000003"), new("provision", "/prəˈvɪʒ.ən/", "noun", "clause; act of supplying", "We added a warranty provision covering replacement parts for two years.", "We added a warranty clause covering replacement parts for two years.", ["contract provision", "under the provisions of", "make provision for"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000004"), new("renew", "/rɪˈnuː/", "verb", "extend (a contract, subscription, license)", "Clients who renew their annual subscription early receive a 15% discount.", "Clients who renew their annual subscription early get a 15% discount.", ["renew a contract", "automatic renewal", "renew a membership"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000005"), new("obligate", "/ˈɑːb.lɪ.ɡeɪt/", "verb", "bind by duty or legal requirement", "The supplier is obligated to deliver all components by Friday.", "The supplier is required to deliver all components by Friday.", ["be obligated to", "contractual obligation", "meet an obligation"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000002-0000-4000-8000-000000000006"), new("amendment", "/əˈmend.mənt/", "noun", "revision, contract addendum", "Any amendment to the service agreement requires written approval.", "Any revision to the service agreement requires written approval.", ["proposed amendment", "make an amendment", "contract amendment"], "Part 6 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000007"), new("consensus", "/kənˈsen.səs/", "noun", "general agreement among a group", "After two hours of negotiation, the committee reached a consensus.", "After two hours of negotiation, the committee reached a general agreement.", ["reach a consensus", "broad consensus", "build consensus"], "Part 3 · Part 7", "C1")),
                (Guid.Parse("10000002-0000-4000-8000-000000000008"), new("terminate", "/ˈtɜːr.mɪ.neɪt/", "verb", "end, cancel (a contract)", "Either company may terminate the partnership upon written notice.", "Either company can end the partnership upon written notice.", ["terminate a contract", "early termination", "termination fee"], "Part 5 · Part 7", "B2")),
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
                (Guid.Parse("10000003-0000-4000-8000-000000000001"), new("reimburse", "/ˌriː.ɪmˈbɜːrs/", "verb", "pay back expenses (travel, purchases)", "Keep all hotel receipts so the accounting team can reimburse you.", "Keep all hotel receipts so the accounting team can pay you back.", ["reimburse expenses", "travel reimbursement", "reimbursement form"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000002"), new("discrepancy", "/dɪˈskrep.ən.si/", "noun", "difference, mismatch (between figures or invoices)", "The auditor noticed a discrepancy between the invoice and the purchase order.", "The auditor spotted a mismatch between the invoice and the purchase order.", ["billing discrepancy", "resolve a discrepancy", "report a discrepancy"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000003"), new("allocate", "/ˈæl.ə.keɪt/", "verb", "assign or distribute (budget, resources)", "The board allocated additional funds to expand the R&D division.", "The board assigned additional funds to expand the R&D division.", ["allocate funds", "budget allocation", "allocate resources"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000004"), new("revenue", "/ˈrev.ə.nuː/", "noun", "total income from sales or services", "Online subscriptions accounted for 40% of our annual revenue.", "Online subscriptions made up 40% of our annual revenue.", ["annual revenue", "generate revenue", "revenue growth"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000005"), new("overdue", "/ˌoʊ.vɚˈduː/", "adjective", "past due (payment, delivery)", "A late fee applies to any balance that is more than 15 days overdue.", "A late fee applies to any balance more than 15 days past due.", ["overdue payment", "overdue invoice", "long overdue"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000006"), new("expenditure", "/ɪkˈspen.dɪ.tʃɚ/", "noun", "amount spent, operating cost", "We need to curb operating expenditure during the third quarter.", "We need to control operating spending during the third quarter.", ["capital expenditure", "reduce expenditure", "public expenditure"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000003-0000-4000-8000-000000000007"), new("forecast", "/ˈfɔːr.kæst/", "noun / verb", "financial projection or prediction", "Analysts forecast steady export demand through the end of the year.", "Analysts predict steady export demand through the end of the year.", ["sales forecast", "financial forecast", "forecast growth"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000003-0000-4000-8000-000000000008"), new("deduct", "/dɪˈdʌkt/", "verb", "subtract from a total amount", "The discount will be automatically deducted at checkout.", "The discount will be subtracted automatically at checkout.", ["deduct from", "tax deduction", "automatically deducted"], "Part 5 · Part 6", "B2")),
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
                (Guid.Parse("10000004-0000-4000-8000-000000000001"), new("complimentary", "/ˌkɑːm.pləˈmen.t̬ɚ.i/", "adjective", "free of charge (provided as a courtesy)", "All hotel guests receive complimentary breakfast and airport shuttle service.", "All hotel guests get free breakfast and airport shuttle service.", ["complimentary breakfast", "complimentary upgrade", "complimentary ticket"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000002"), new("occupancy", "/ˈɑːk.jə.pən.si/", "noun", "room occupancy rate, maximum capacity", "Peak-season occupancy at the resort reached 95% last July.", "Peak-season room occupancy at the resort reached 95% last July.", ["occupancy rate", "maximum occupancy", "double occupancy"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000003"), new("depart", "/dɪˈpɑːrt/", "verb", "leave, set off from a station or gate", "The express train to Kyoto departs from Platform 4 in ten minutes.", "The express train to Kyoto leaves from Platform 4 in ten minutes.", ["departure gate", "depart on schedule", "departure lounge"], "Part 2 · Part 4", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000004"), new("amenity", "/əˈmen.ə.t̬i/", "noun", "useful facility or guest convenience", "Modern amenities include high-speed Wi-Fi and a 24-hour fitness center.", "Modern facilities include high-speed Wi-Fi and a 24-hour fitness center.", ["hotel amenities", "local amenities", "on-site amenities"], "Part 4 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000005"), new("confirm", "/kənˈfɜːrm/", "verb", "verify or finalize (a booking, appointment)", "Could you confirm my reservation for two nights starting October 12?", "Could you verify my two-night reservation starting October 12?", ["confirm a reservation", "confirmation number", "written confirmation"], "Part 2 · Part 3", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000006"), new("layover", "/ˈleɪ.oʊ.vɚ/", "noun", "stopover time between connecting flights", "Because of the three-hour layover in Seoul, I had time to answer emails.", "Because of the three-hour stopover in Seoul, I had time to reply to emails.", ["short layover", "overnight layover", "long layover"], "Part 3 · Part 7", "B2")),
                (Guid.Parse("10000004-0000-4000-8000-000000000007"), new("beverage", "/ˈbev.ɚ.ɪdʒ/", "noun", "drink, refreshment", "Light snacks and beverages will be served in the lobby during intermission.", "Light snacks and drinks will be served in the lobby during intermission.", ["food and beverage", "refreshing beverage", "complimentary beverages"], "Part 1 · Part 4", "B1")),
                (Guid.Parse("10000004-0000-4000-8000-000000000008"), new("adjacent", "/əˈdʒeɪ.sənt/", "adjective", "next to, right beside", "Parking is available in the garage adjacent to the convention center.", "Parking is available in the garage right next to the convention center.", ["adjacent to", "adjacent building", "adjacent rooms"], "Part 1 · Part 5", "B2")),
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
                (Guid.Parse("10000005-0000-4000-8000-000000000001"), new("merchandise", "/ˈmɜːr.tʃən.daɪs/", "noun", "goods for sale, retail products", "Seasonal merchandise is marked down by 30% until Sunday.", "Seasonal goods are discounted by 30% through Sunday.", ["defective merchandise", "display merchandise", "promotional merchandise"], "Part 1 · Part 7", "B2")),
                (Guid.Parse("10000005-0000-4000-8000-000000000002"), new("demographics", "/ˌdem.əˈɡræf.ɪks/", "noun", "population segments (age, income groups)", "The campaign was tailored to younger urban demographics.", "The campaign was tailored for younger urban customer groups.", ["target demographics", "customer demographics", "demographic data"], "Part 4 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000003"), new("incentive", "/ɪnˈsen.tɪv/", "noun", "motivational reward or promotional offer", "Free express shipping served as a strong incentive for repeat orders.", "Free express shipping was a strong motivation for repeat orders.", ["financial incentive", "offer an incentive", "sales incentive"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000005-0000-4000-8000-000000000004"), new("launch", "/lɔːntʃ/", "verb / noun", "introduce to the market (product, campaign)", "The company plans to launch its eco-friendly packaging line in May.", "The company plans to introduce its eco-friendly packaging line in May.", ["product launch", "launch a campaign", "launch event"], "Part 3 · Part 6", "B1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000005"), new("endorse", "/ɪnˈdɔːrs/", "verb", "publicly support or promote a product", "Several professional athletes endorse our new running shoes.", "Several professional athletes promote our new running shoes.", ["celebrity endorsement", "officially endorse", "endorse a product"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000006"), new("competitive", "/kəmˈpet̬.ə.t̬ɪv/", "adjective", "strongly comparable in price or quality", "We maintain competitive pricing without compromising material quality.", "We keep our prices competitive without sacrificing material quality.", ["competitive edge", "competitive pricing", "highly competitive"], "Part 5 · Part 6", "B1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000007"), new("outperform", "/ˌaʊt.pɚˈfɔːrm/", "verb", "do better than, achieve higher results", "The updated model outperformed rival devices in battery life tests.", "The updated model beat competing devices in battery life tests.", ["outperform competitors", "outperform expectations", "outperform the market"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000005-0000-4000-8000-000000000008"), new("survey", "/ˈsɜːr.veɪ/", "noun / verb", "customer feedback questionnaire", "Please take two minutes to complete our customer satisfaction survey.", "Please take two minutes to complete our customer satisfaction questionnaire.", ["conduct a survey", "survey results", "satisfaction survey"], "Part 4 · Part 7", "B1")),
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
                (Guid.Parse("10000006-0000-4000-8000-000000000001"), new("applicant", "/ˈæp.lə.kənt/", "noun", "person applying for a job", "Successful applicants will be contacted for a second-round interview.", "Qualified candidates will be contacted for a second-round interview.", ["job applicant", "qualified applicant", "screen applicants"], "Part 5 · Part 7", "B1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000002"), new("credentials", "/krɪˈden.ʃəlz/", "noun (plural)", "degrees, certifications, and qualifications", "The hiring panel verified all academic credentials prior to making an offer.", "The hiring panel checked all academic qualifications before making an offer.", ["verify credentials", "professional credentials", "strong credentials"], "Part 5 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000003"), new("probationary", "/proʊˈbeɪ.ʃə.ner.i/", "adjective", "relating to a trial employment period", "Health benefits begin immediately after the 60-day probationary period.", "Health benefits start right after the 60-day trial period.", ["probationary period", "probationary employee", "complete probation"], "Part 6 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000004"), new("proficient", "/prəˈfɪʃ.ənt/", "adjective", "skilled, highly competent in a field", "Candidates must be proficient in spreadsheet modeling and data visualization.", "Candidates must be skilled in spreadsheet modeling and data visualization.", ["proficient in", "highly proficient", "language proficiency"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000005"), new("appraisal", "/əˈpreɪ.zəl/", "noun", "work performance evaluation", "Annual performance appraisals are scheduled for the first week of December.", "Annual performance evaluations are scheduled for the first week of December.", ["performance appraisal", "conduct an appraisal", "annual appraisal"], "Part 3 · Part 7", "C1")),
                (Guid.Parse("10000006-0000-4000-8000-000000000006"), new("vacancy", "/ˈveɪ.kən.si/", "noun", "open job position", "Internal staff are encouraged to apply for the senior analyst vacancy.", "Internal staff are encouraged to apply for the open senior analyst position.", ["fill a vacancy", "job vacancy", "current vacancies"], "Part 5 · Part 7", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000007"), new("eligible", "/ˈel.ə.dʒə.bəl/", "adjective", "qualified or entitled to receive benefits", "Full-time employees are eligible for tuition reimbursement after one year.", "Full-time employees qualify for tuition reimbursement after one year.", ["be eligible for", "eligible candidates", "eligibility criteria"], "Part 5 · Part 6", "B2")),
                (Guid.Parse("10000006-0000-4000-8000-000000000008"), new("mentor", "/ˈmen.tɔːr/", "noun / verb", "experienced advisor; guide and coach", "Each new hire is paired with an experienced mentor during onboarding.", "Each new hire is paired with an experienced advisor during onboarding.", ["mentorship program", "assign a mentor", "mentor junior staff"], "Part 3 · Part 7", "B2")),
            ],
            SeedTimestamp),
    ];
}
