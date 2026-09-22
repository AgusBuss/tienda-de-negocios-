namespace InventarioAPI.DTOs
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PaginaActual { get; set; }
        public int TamanioPagina { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalPaginas { get; set; }
    }
}