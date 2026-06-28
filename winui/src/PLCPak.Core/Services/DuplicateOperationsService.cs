using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DuplicateOperationsService
{
    public static DuplicateOperationsBundle BuildBundle(IEnumerable<PublishJob> jobs)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var scan = DuplicateScanService.Scan(list);
        var suggestions = DuplicateMergeSuggestionService.BuildSuggestions(list, scan);

        return new DuplicateOperationsBundle
        {
            Scan = scan,
            Suggestions = suggestions
        };
    }
}