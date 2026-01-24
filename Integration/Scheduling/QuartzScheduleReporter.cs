using System;
using Quartz;

namespace Integration.Scheduling;

public static class QuartzScheduleReporter
{
    public static ITrigger BuildTrigger(string agentId, Integration.Services.ScheduleBlock schedule)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        if (schedule is null)
            throw new ArgumentNullException(nameof(schedule));

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"{agentId}.trigger", "agents");

        // Приоритет:
        // 1) every_seconds
        // 2) every_minutes
        // 3) every_hours
        // 4) daily_at (HH:mm)

        if (schedule.Every_Seconds is > 0)
        {
            triggerBuilder = triggerBuilder
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(schedule.Every_Seconds.Value)
                    .RepeatForever());
        }
        else if (schedule.Every_Minutes is > 0)
        {
            triggerBuilder = triggerBuilder
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(schedule.Every_Minutes.Value)
                    .RepeatForever());
        }
        else if (schedule.Every_Hours is > 0)
        {
            triggerBuilder = triggerBuilder
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(schedule.Every_Hours.Value)
                    .RepeatForever());
        }
        else if (!string.IsNullOrWhiteSpace(schedule.Daily_At))
        {
            // формат HH:mm, локальное время
            if (!TimeSpan.TryParse(schedule.Daily_At, out var time))
                throw new InvalidOperationException($"Invalid daily_at format: '{schedule.Daily_At}'");

            // daily at HH:mm
            var cron = $"0 {time.Minutes} {time.Hours} ? * *";

            // ВАЖНО: для cron НЕ делаем StartNow(), чтобы не было "лишнего" запуска при старте.
            triggerBuilder = triggerBuilder.WithCronSchedule(cron);
        }
        else
        {
            // fallback — защита от пустого расписания
            triggerBuilder = triggerBuilder
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(10)
                    .RepeatForever());
        }

        return triggerBuilder.Build();
    }

    public static string Describe(ITrigger? trigger)
    {
        if (trigger is null)
            return "—";

        if (trigger is ISimpleTrigger st)
        {
            var ts = st.RepeatInterval;

            if (ts.TotalHours >= 1 && ts.TotalHours % 1 == 0)
                return $"every {(int)ts.TotalHours} h";

            if (ts.TotalMinutes >= 1 && ts.TotalMinutes % 1 == 0)
                return $"every {(int)ts.TotalMinutes} min";

            if (ts.TotalSeconds >= 1 && ts.TotalSeconds % 1 == 0)
                return $"every {(int)ts.TotalSeconds} s";

            return $"every {ts}";
        }

        if (trigger is ICronTrigger ct)
        {
            // Пытаемся распознать наш "daily_at": "0 M H ? * *"
            if (TryParseDailyAtCron(ct.CronExpressionString, out var hhmm))
                return $"every {hhmm}";

            return ct.CronExpressionString;
        }

        return trigger.GetType().Name;
    }

    private static bool TryParseDailyAtCron(string cron, out string hhmm)
    {
        hhmm = "";

        // ожидаем: "0 M H ? * *"
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6)
            return false;

        if (parts[0] != "0" || parts[3] != "?" || parts[4] != "*" || parts[5] != "*")
            return false;

        if (!int.TryParse(parts[1], out var minute)) return false;
        if (!int.TryParse(parts[2], out var hour)) return false;

        if (hour < 0 || hour > 23) return false;
        if (minute < 0 || minute > 59) return false;

        hhmm = $"{hour:00}:{minute:00}";
        return true;
    }

    // summary: QuartzScheduleReporter — построение Trigger из конфигурации расписания и генерация
    //          человекочитаемого описания ("every 10 min", "every 22:00") для отображения в UI.
}
