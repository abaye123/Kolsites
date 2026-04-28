using System;
using System.Collections.Generic;
using System.Linq;

namespace Kolsites
{
    /// <summary>
    /// בודק אם המועד הנוכחי נופל בתוך אחד מטווחי הזמן החסומים שהוגדרו.
    /// תומך בטווחים שחוצים חצות (EndTime &lt; StartTime).
    /// </summary>
    public static class BlockedTimeChecker
    {
        public class BlockResult
        {
            public BlockedTimeRange Range { get; init; } = new();
            /// <summary>הזמן (אבסולוטי) שבו הטווח החסום מסתיים</summary>
            public DateTime EndsAt { get; init; }
            public TimeSpan Remaining => EndsAt - DateTime.Now;
        }

        /// <summary>מחזיר את הטווח החסום הפעיל כעת, או null אם אין כזה.</summary>
        public static BlockResult? GetActiveBlock(IEnumerable<BlockedTimeRange> ranges, DateTime now)
        {
            int dow = (int)now.DayOfWeek; // 0=Sunday ... 6=Saturday
            int yesterdayDow = (dow + 6) % 7;
            var nowTime = now.TimeOfDay;

            foreach (var range in ranges.Where(r => r.Enabled && r.Days.Count > 0))
            {
                bool crossesMidnight = range.EndTime <= range.StartTime;

                // טווח שמתחיל היום
                if (range.Days.Contains(dow))
                {
                    if (!crossesMidnight)
                    {
                        // טווח רגיל באותו יום: nowTime בין Start ל-End
                        if (nowTime >= range.StartTime && nowTime < range.EndTime)
                        {
                            return new BlockResult
                            {
                                Range = range,
                                EndsAt = now.Date.Add(range.EndTime)
                            };
                        }
                    }
                    else
                    {
                        // טווח חוצה חצות: מספיק שאנחנו אחרי StartTime היום -
                        // הסיום יהיה בבוקר של מחר.
                        if (nowTime >= range.StartTime)
                        {
                            return new BlockResult
                            {
                                Range = range,
                                EndsAt = now.Date.AddDays(1).Add(range.EndTime)
                            };
                        }
                    }
                }

                // טווח חוצה חצות שהתחיל אתמול וגלש להיום
                if (crossesMidnight && range.Days.Contains(yesterdayDow))
                {
                    if (nowTime < range.EndTime)
                    {
                        return new BlockResult
                        {
                            Range = range,
                            EndsAt = now.Date.Add(range.EndTime)
                        };
                    }
                }
            }

            return null;
        }
    }
}
