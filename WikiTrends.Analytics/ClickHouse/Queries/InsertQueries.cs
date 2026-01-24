namespace WikiTrends.Analytics.ClickHouse.Queries;

public static class InsertQueries
{
    public static string GetInsertEditQuery()
    {
        //  1. Вернуть INSERT SQL для записи ClassifiedEditEvent в таблицу событий
        //  2. Предусмотреть формат CSV/JSONEachRow для HTTP интерфейса ClickHouse
        return """

            """;
    }

    public static string GetUpsertBaselineQuery()
    {
        //  1. Вернуть INSERT SQL для записи ClassifiedEditEvent в таблицу событий
        //  2. Предусмотреть формат CSV/JSONEachRow для HTTP интерфейса ClickHouse
        return """
                INSERT INTO BaselineData
            (
                TopicId, 
                BaselineDaily, 
                CalculatedAt
            ) 
            VALUES 
            (
                @topicId, 
                @baselineDaily,
                @calcAt
            )
            """;
    }
}
