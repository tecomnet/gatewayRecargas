using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GateWayRecargas.Filters;

public class SwaggerTagOrderDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Definir el orden deseado de los tags (por nombre de controlador o tag)
        var tagOrder = new Dictionary<string, int>
        {
            // Autenticación JWT - Prioridad 1
            { "1. Autenticación JWT", 1 },
            { "Auth", 1 },
            { "Setup", 1 },
            
            // Altan - Prioridad 2
            { "2. Altan - Ofertas", 2 },
            { "AltanOffer", 2 },
            { "2. Altan - Recargas", 2 },
            { "Product", 2 },
            { "2. Altan - MSISDN", 2 },
            { "Msisdn", 2 },
            { "2. Altan - Token", 2 },
            { "Token", 2 },
            
            // Reportes - Prioridad 3
            { "3. Reportes", 3 },
            { "Reporte", 3 }
        };

        // Ordenar los tags según el diccionario
        if (swaggerDoc.Tags != null)
        {
            swaggerDoc.Tags = swaggerDoc.Tags
                .OrderBy(tag =>
                {
                    // Buscar el orden del tag por nombre exacto
                    if (tagOrder.TryGetValue(tag.Name, out var order))
                    {
                        return order;
                    }
                    
                    // Buscar por coincidencia parcial
                    var match = tagOrder.FirstOrDefault(kvp => 
                        tag.Name.Contains(kvp.Key) || kvp.Key.Contains(tag.Name));
                    
                    return match.Value != 0 ? match.Value : 99;
                })
                .ThenBy(tag => tag.Name)
                .ToList();
        }
    }
}

