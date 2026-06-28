using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class WorkflowChainService
{
    public const int MaxIterations = 10;

    private static readonly HashSet<JobNextActionType> ManualStopActions =
    [
        JobNextActionType.FillLinks,
        JobNextActionType.MarkPublished,
        JobNextActionType.SendTelegram
    ];

    public static bool HasAutomatableFirstAction(PublishJob job)
        => JobHealthService.HasAutomatableChainAction(job);

    public static async Task<BatchChainResult> RunBatchAutomatableChainAsync(
        IEnumerable<PublishJob> jobs,
        Func<PublishJob, Task<WorkflowChainResult>> runChainForJobAsync,
        CancellationToken ct = default)
    {
        var result = new BatchChainResult();

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();

            if (!HasAutomatableFirstAction(job))
            {
                result.Skipped++;
                var label = PublishWorkflowService.GetNextActionForJob(job)?.ActionLabel ?? "无下一步";
                result.Messages.Add($"[跳过] {job.Title}: 下一步需手动操作 ({label})");
                continue;
            }

            var chainResult = await runChainForJobAsync(job).ConfigureAwait(false);
            result.JobResults.Add(chainResult);

            if (!chainResult.Success)
            {
                result.Failed++;
                result.Messages.Add($"[失败] {job.Title}: {chainResult.Message}");
            }
            else if (chainResult.NeedsUserInput)
            {
                result.StoppedForManual++;
                result.Messages.Add($"[待手动] {job.Title}: {chainResult.Message}");
            }
            else
            {
                result.Success++;
                result.Messages.Add($"[成功] {job.Title}");
            }
        }

        return result;
    }

    public static async Task<WorkflowChainResult> RunAutomatableChainAsync(
        PublishJob job,
        Func<JobNextActionType, Task<JobNextActionResult>> executeActionAsync,
        Func<string, PublishJob?> reloadJob,
        CancellationToken ct = default)
    {
        var steps = new List<WorkflowChainStepResult>();
        var current = job;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            current = reloadJob(current.Id) ?? current;
            var entry = PublishWorkflowService.GetNextActionForJob(current);
            var action = entry?.Action;

            if (action is null or JobNextActionType.None or JobNextActionType.FillLinks)
            {
                var message = action == JobNextActionType.FillLinks
                    ? "需要手动回填链接"
                    : "需要手动回填链接";
                steps.Add(new WorkflowChainStepResult
                {
                    Step = entry?.ActionLabel ?? "无下一步",
                    Success = true,
                    Stopped = true,
                    Message = message,
                    Action = action
                });

                return BuildResult(
                    current,
                    steps,
                    success: true,
                    needsUserInput: true,
                    message,
                    stopReason: action?.ToString() ?? "None");
            }

            if (ManualStopActions.Contains(action.Value))
            {
                var message = action.Value switch
                {
                    JobNextActionType.MarkPublished => "需要手动确认发布状态",
                    JobNextActionType.SendTelegram => "需要手动发送 Telegram",
                    _ => "需要手动回填链接"
                };

                steps.Add(new WorkflowChainStepResult
                {
                    Step = entry!.ActionLabel,
                    Success = true,
                    Stopped = true,
                    Message = message,
                    Action = action
                });

                return BuildResult(
                    current,
                    steps,
                    success: true,
                    needsUserInput: true,
                    message,
                    stopReason: action.Value.ToString());
            }

            var result = await executeActionAsync(action.Value).ConfigureAwait(false);
            current = reloadJob(current.Id) ?? result.Job ?? current;

            steps.Add(new WorkflowChainStepResult
            {
                Step = entry!.ActionLabel,
                Success = result.Success,
                Stopped = false,
                Message = result.Message,
                Action = action
            });

            if (!result.Success)
            {
                return BuildResult(
                    current,
                    steps,
                    success: false,
                    needsUserInput: result.NeedsUserInput,
                    result.Message,
                    stopReason: "ActionFailed");
            }
        }

        return BuildResult(
            current,
            steps,
            success: true,
            needsUserInput: false,
            $"已达到最大自动步骤数 ({MaxIterations})",
            stopReason: "MaxIterations");
    }

    private static WorkflowChainResult BuildResult(
        PublishJob job,
        List<WorkflowChainStepResult> steps,
        bool success,
        bool needsUserInput,
        string message,
        string? stopReason)
        => new()
        {
            Success = success,
            NeedsUserInput = needsUserInput,
            Message = message,
            Steps = steps,
            Job = job,
            StopReason = stopReason
        };
}