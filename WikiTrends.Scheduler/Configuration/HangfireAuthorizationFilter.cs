namespace WikiTrends.Scheduler.Configuration;

public sealed class HangfireAuthorizationFilter
{
    public bool Authorize(object context)
    {
        // TODO: 1. Проверить, что пользователь аутентифицирован/авторизован
        // TODO: 2. Разрешить доступ к Hangfire Dashboard только admin ролям
        // TODO: 3. Вернуть true/false
        return true;
    }
}
