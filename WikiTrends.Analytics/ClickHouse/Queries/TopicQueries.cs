using System;
using System.Collections.Generic;
using System.Linq;

namespace WikiTrends.Analytics.ClickHouse.Queries;

public static class TopicQueries
{
    public static string GetTopicInfoQuery(IReadOnlyCollection<int> topicIds)
    {
        var ids = topicIds?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
        var inList = ids.Length == 0 ? "0" : string.Join(",", ids);

        return $@"
            SELECT
                tid,
                coalesce(
                    argMaxIf(name, greatest(Timestamp, ClassifiedAt), name != '' AND NOT match(lower(name), '^topic\\s*\\d+$') AND NOT match(lower(name), '^topic\\d+$')),
                    argMax(name, greatest(Timestamp, ClassifiedAt))
                ) as name,
                coalesce(
                    argMaxIf(path, greatest(Timestamp, ClassifiedAt), path != '' AND NOT match(lower(path), '^topic-\\d+$')),
                    argMax(path, greatest(Timestamp, ClassifiedAt))
                ) as path
            FROM
            (
                SELECT
                    tid,
                    name,
                    path,
                    Timestamp,
                    ClassifiedAt
                FROM edit_events
                ARRAY JOIN
                    Topics.TopicId AS tid,
                    Topics.Name AS name,
                    Topics.Path AS path
                WHERE tid IN ({inList})
            )
            GROUP BY tid";
    }
}
