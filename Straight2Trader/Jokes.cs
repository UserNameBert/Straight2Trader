using System;
using System.Collections.Generic;

namespace Straight2Trader
{
    internal class Jokes
    {
        public string RandomMessage()
        {
            var messages = new List<string>
            {
                "Cargo balanced. Debits and credits agree. Probably.",
                "Your cargo’s net worth is healthier than my student loan balance.",
                "Filed under: ‘Definitely not tax-deductible.’",
                "Remember: hiding cargo from Customs is not a recognized accounting strategy.",
                "Accounted for every SCU. Unlike my sleep schedule.",
                "Your ledger is cleaner than a fresh audit report... for now.",
                "Hauling cargo and emotional baggage. Just another Tuesday.",
                "Cash flow positive. Unlike your ex's crypto portfolio.",
                "Another haul, another spreadsheet that cries itself to sleep.",
                "Profit margin: juicy. Risk tolerance: questionable.",
                "Cargo secured. Unlike my financial future.",
                "Warning: This haul may trigger your accountant's calculator.",
                "This manifest was audited. Once. Briefly.",
                "Cargo classified as: 'miscellaneous assets' to avoid questions.",
                "Declared value: plausible. Paper trail: questionable.",
                "We round to the nearest lie, as per standard accounting practices.",
                "Cargo weight may vary. Especially after lunch.",
                "All SCU accounted for, except the ones that mysteriously vanished at Port Olisar.",
                "Filed this under 'hope' and 'wishful thinking'.",
                "Double-entry bookkeeping? Nah, this was triple-guessed.",
                "Cargo protected by tax loopholes and duct tape.",
                "Estimated value: legally optimistic.",
                "Inventory validated by an intern who 'thinks' it's correct.",
                "Warning: cargo contents may include financial regret.",
                "Declared income: yes. Declared liability: also yes.",
                "We tried reconciling this ledger but it fought back.",
                "Budget approved by a raccoon accountant in a mining helmet.",
                "If you can’t track it, it’s not taxable. Probably.",
                "Profit is temporary. Paperwork is forever.",
                "This receipt is valid until the next fiscal crisis.",
                "Liability: low. Suspicion: high.",
                "This log brought to you by caffeine, chaos, and questionable math.",
                "All cargo was legally acquired? Ish.",
                "Receipts? Who even...",
                "Auditor note: nice try."

            };
            var random = new Random();
            return messages[random.Next(messages.Count)];
        }
    }
}
