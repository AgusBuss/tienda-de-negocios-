using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventarioAPI.Data;
using InventarioAPI.Models;

namespace InventarioAPI.Services
{
    public class ReporteService
    {
        private readonly AppDbContext _context;

        // Productos con este stock o menos se resaltan en rojo en el reporte
        private const int UmbralStockBajo = 5;

        private static readonly CultureInfo Cultura = new("es-AR");

        static ReporteService()
        {
            // Licencia gratuita de QuestPDF (uso académico / proyectos chicos)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ReporteService(AppDbContext context)
        {
            _context = context;
        }

        private static string Dinero(decimal valor) => "$ " + valor.ToString("N2", Cultura);

        // ---------------------------------------------------------------
        // REPORTE 1: stock actual
        // ---------------------------------------------------------------
        public async Task<byte[]> StockAsync()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Nombre,
                    Categoria = p.Categoria!.Nombre,
                    p.Precio,
                    p.Stock
                })
                .ToListAsync();

            var filas = productos.Select(p => new[]
            {
                p.Nombre,
                p.Categoria,
                Dinero(p.Precio),
                p.Stock.ToString(),
                Dinero(p.Precio * p.Stock)
            }).ToList();

            var unidades = productos.Sum(p => p.Stock);
            var valorTotal = productos.Sum(p => p.Precio * p.Stock);
            var stockBajo = productos.Count(p => p.Stock <= UmbralStockBajo);

            return Documento(
                "Reporte de stock actual",
                $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}",
                contenido =>
                {
                    contenido.Column(col =>
                    {
                        col.Item().Element(c => Tabla(
                            c,
                            new[] { "Producto", "Categoría", "Precio", "Stock", "Valor en stock" },
                            new float[] { 3, 2, 1.3f, 0.8f, 1.5f },
                            new[] { false, false, true, true, true },
                            filas,
                            fila => productos[fila].Stock <= UmbralStockBajo));

                        col.Item().PaddingTop(12).Column(res =>
                        {
                            res.Item().Text($"Productos activos: {productos.Count}");
                            res.Item().Text($"Unidades en stock: {unidades}");
                            res.Item().Text($"Productos con stock bajo (≤ {UmbralStockBajo}, resaltados): {stockBajo}");
                            res.Item().Text($"Valor total del inventario: {Dinero(valorTotal)}").SemiBold();
                        });
                    });
                });
        }

        // ---------------------------------------------------------------
        // REPORTE 2: entradas de mercadería
        // ---------------------------------------------------------------
        public async Task<byte[]> EntradasAsync(DateTime desde, DateTime hasta)
        {
            var inicio = desde.Date;
            var fin = hasta.Date.AddDays(1);

            var lineas = await _context.IngresoDetalles
                .AsNoTracking()
                .Where(d => d.Ingreso!.Fecha >= inicio && d.Ingreso.Fecha < fin)
                .OrderBy(d => d.Ingreso!.Fecha)
                .Select(d => new
                {
                    IngresoId = d.IngresoId,
                    Fecha = d.Ingreso!.Fecha,
                    Proveedor = d.Ingreso.Proveedor!.Nombre,
                    Producto = d.Producto!.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario
                })
                .ToListAsync();

            var filas = lineas.Select(l => new[]
            {
                l.Fecha.ToString("dd/MM/yyyy HH:mm"),
                $"#{l.IngresoId}",
                l.Proveedor,
                l.Producto,
                l.Cantidad.ToString(),
                Dinero(l.PrecioUnitario),
                Dinero(l.Cantidad * l.PrecioUnitario)
            }).ToList();

            var cantidadIngresos = lineas.Select(l => l.IngresoId).Distinct().Count();
            var unidades = lineas.Sum(l => l.Cantidad);
            var total = lineas.Sum(l => l.Cantidad * l.PrecioUnitario);

            return Documento(
                "Reporte de entradas de mercadería",
                $"Período: {inicio:dd/MM/yyyy} al {hasta.Date:dd/MM/yyyy}  |  Generado el {DateTime.Now:dd/MM/yyyy HH:mm}",
                contenido =>
                {
                    contenido.Column(col =>
                    {
                        col.Item().Element(c => Tabla(
                            c,
                            new[] { "Fecha", "Ingreso", "Proveedor", "Producto", "Cant.", "Precio unit.", "Subtotal" },
                            new float[] { 1.8f, 0.9f, 2, 2.2f, 0.8f, 1.4f, 1.5f },
                            new[] { false, false, false, false, true, true, true },
                            filas));

                        col.Item().PaddingTop(12).Column(res =>
                        {
                            res.Item().Text($"Ingresos registrados: {cantidadIngresos}");
                            res.Item().Text($"Unidades ingresadas: {unidades}");
                            res.Item().Text($"Costo total de las entradas: {Dinero(total)}").SemiBold();
                        });
                    });
                });
        }

        // ---------------------------------------------------------------
        // REPORTE 3: salidas (ventas)
        // ---------------------------------------------------------------
        public async Task<byte[]> SalidasAsync(DateTime desde, DateTime hasta)
        {
            var inicio = desde.Date;
            var fin = hasta.Date.AddDays(1);

            var lineas = await _context.SalidaDetalles
                .AsNoTracking()
                .Where(d => d.Salida!.Fecha >= inicio && d.Salida.Fecha < fin)
                .OrderBy(d => d.Salida!.Fecha)
                .Select(d => new
                {
                    SalidaId = d.SalidaId,
                    Fecha = d.Salida!.Fecha,
                    Cliente = d.Salida.Cliente!.Nombre,
                    Producto = d.Producto!.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario,
                    Metodo = d.Salida.Pago != null ? (MetodoPago?)d.Salida.Pago.Metodo : null
                })
                .ToListAsync();

            var filas = lineas.Select(l => new[]
            {
                l.Fecha.ToString("dd/MM/yyyy HH:mm"),
                $"#{l.SalidaId}",
                l.Cliente,
                l.Producto,
                l.Cantidad.ToString(),
                Dinero(l.PrecioUnitario),
                Dinero(l.Cantidad * l.PrecioUnitario),
                l.Metodo?.ToString() ?? "Mostrador"
            }).ToList();

            var cantidadVentas = lineas.Select(l => l.SalidaId).Distinct().Count();
            var unidades = lineas.Sum(l => l.Cantidad);
            var total = lineas.Sum(l => l.Cantidad * l.PrecioUnitario);

            return Documento(
                "Reporte de salidas (ventas)",
                $"Período: {inicio:dd/MM/yyyy} al {hasta.Date:dd/MM/yyyy}  |  Generado el {DateTime.Now:dd/MM/yyyy HH:mm}",
                contenido =>
                {
                    contenido.Column(col =>
                    {
                        col.Item().Element(c => Tabla(
                            c,
                            new[] { "Fecha", "Venta", "Cliente", "Producto", "Cant.", "Precio unit.", "Subtotal", "Pago" },
                            new float[] { 1.7f, 0.8f, 1.8f, 2, 0.7f, 1.3f, 1.4f, 1.3f },
                            new[] { false, false, false, false, true, true, true, false },
                            filas));

                        col.Item().PaddingTop(12).Column(res =>
                        {
                            res.Item().Text($"Ventas registradas: {cantidadVentas}");
                            res.Item().Text($"Unidades vendidas: {unidades}");
                            res.Item().Text($"Total vendido: {Dinero(total)}").SemiBold();
                        });
                    });
                });
        }

        // ---------------------------------------------------------------
        // Piezas comunes de dibujo
        // ---------------------------------------------------------------

        // Arma la hoja A4 con encabezado y pie de página
        private static byte[] Documento(string titulo, string subtitulo, Action<IContainer> contenido)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(titulo).FontSize(18).SemiBold();
                        col.Item().Text(subtitulo).FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingVertical(10).Element(contenido);

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        // Dibuja una tabla genérica. "resaltar" recibe el índice de la fila y devuelve true para pintarla en rojo suave.
        private static void Tabla(
            IContainer container,
            string[] titulos,
            float[] anchos,
            bool[] alinearDerecha,
            List<string[]> filas,
            Func<int, bool>? resaltar = null)
        {
            if (filas.Count == 0)
            {
                container.Text("No hay registros para mostrar.").Italic();
                return;
            }

            container.Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    foreach (var ancho in anchos)
                        columnas.RelativeColumn(ancho);
                });

                tabla.Header(encabezado =>
                {
                    for (int i = 0; i < titulos.Length; i++)
                    {
                        var celda = encabezado.Cell().Background(Colors.Grey.Lighten2).Padding(4);
                        if (alinearDerecha[i])
                            celda.AlignRight().Text(titulos[i]).SemiBold();
                        else
                            celda.Text(titulos[i]).SemiBold();
                    }
                });

                for (int f = 0; f < filas.Count; f++)
                {
                    var fondo = resaltar != null && resaltar(f) ? Colors.Red.Lighten4 : Colors.White;

                    for (int i = 0; i < filas[f].Length; i++)
                    {
                        var celda = tabla.Cell()
                            .Background(fondo)
                            .BorderBottom(0.5f)
                            .BorderColor(Colors.Grey.Lighten1)
                            .Padding(4);

                        if (alinearDerecha[i])
                            celda.AlignRight().Text(filas[f][i]);
                        else
                            celda.Text(filas[f][i]);
                    }
                }
            });
        }
    }
}