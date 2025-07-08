namespace UrlShortenerSystem.Common.CrossCutting.Dtos
{
    public enum CrudOperationResultStatus
    {
        Success,
        Error,
        NotFound,
        ValidationError
    }
}