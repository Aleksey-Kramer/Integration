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

        //Начало изменений

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"{agentId}.trigger", "agents")
            .StartNow();

        // Приоритет:
        // 1) every_seconds
        // 2) every_minutes
        // 3) every_hours
        // 4) daily_at (HH:mm)

        if (schedule.Every_Seconds is > 0)
        {
            triggerBuilder = triggerBuilder.WithSimpleSchedule(x =>
                x.WithIntervalInSeconds(schedule.Every_Seconds.Value)
                    .RepeatForever());
        }
        else if (schedule.Every_Minutes is > 0)
        {
            triggerBuilder = triggerBuilder.WithSimpleSchedule(x =>
                x.WithIntervalInMinutes(schedule.Every_Minutes.Value)
                    .RepeatForever());
        }
        else if (schedule.Every_Hours is > 0)
        {
            triggerBuilder = triggerBuilder.WithSimpleSchedule(x =>
                x.WithIntervalInHours(schedule.Every_Hours.Value)
                    .RepeatForever());
        }
        else if (!string.IsNullOrWhiteSpace(schedule.Daily_At))
        {
            // формат HH:mm, локальное время
            if (!TimeSpan.TryParse(schedule.Daily_At, out var time))
                throw new InvalidOperationException($"Invalid daily_at format: '{schedule.Daily_At}'");

            var cron = $"0 {time.Minutes} {time.Hours} ? * *";

            triggerBuilder = triggerBuilder.WithCronSchedule(cron);
        }
        else
        {
            // fallback — защита от пустого расписания
            triggerBuilder = triggerBuilder.WithSimpleSchedule(x =>
                x.WithIntervalInMinutes(10)
                    .RepeatForever());
        }

        return triggerBuilder.Build();

        //Конец изменений
    }

    public static string Describe(ITrigger? trigger)
    {
        //Начало изменений

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
            // Для UI лучше показывать человекочитаемое время, а не cron-строку.
            // Минимально: если это daily_at HH:mm (будет позже) — распарсим отдельно.
            // Пока вернём cron string.
            return ct.CronExpressionString;
        }

        return trigger.GetType().Name;

        //Конец изменений
    }

    // summary: QuartzScheduleReporter — построение Trigger из конфигурации расписания и генерация
    //          человекочитаемого описания ("every 10 min", "every 22:00") для отображения в UI.
}
