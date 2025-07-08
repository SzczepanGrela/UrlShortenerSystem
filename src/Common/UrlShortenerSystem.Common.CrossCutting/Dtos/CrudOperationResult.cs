namespace UrlShortenerSystem.Common.CrossCutting.Dtos
{
    public class CrudOperationResult<T>
    {
        public T? Result { get; set; }
        public CrudOperationResultStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }
}