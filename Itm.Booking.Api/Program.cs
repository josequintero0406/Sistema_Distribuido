using Itm.Discount.Api.DTOs;
using Itm.Event.Api.DTOs;
using Microsoft.Extensions.Http.Resilience;
using System.Net;
using System.Net.Http.Json;

// ORQUESTADOR DE SERVICIOS: API DE RESERVAS DE BOLETOS PARA CONCIERTOS
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("EventClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5218");
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("DiscountClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5297");
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddStandardResilienceHandler();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoint para reservar boletos con posible descuento
app.MapPost("/api/bookings", async (BookingRequest request, IHttpClientFactory factory) =>
{
    var eventClient = factory.CreateClient("EventClient");
    var discountClient = factory.CreateClient("DiscountClient");

    // 1. INICIAR LAS PETICIONES EN PARALELO SIN ESPERA
    var eventRequestTask = eventClient.GetAsync($"/api/events/{request.EventId}");
    var discountRequestTask = discountClient.GetAsync($"/api/discounts/{request.DiscountCode}");

    // 2. ESPERAR AMBAS RESPUESTAS
    await Task.WhenAll(eventRequestTask, discountRequestTask);

    // 3. OBTENER LOS RESULTADOS
    var eventResponse = await eventRequestTask;
    var discountResponse = await discountRequestTask;

    // 4. VALIDAR RESPUESTA DEL EVENTO
    if (!eventResponse.IsSuccessStatusCode)
        return Results.BadRequest("No se pudo obtener el evento o este no existe.");

    var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventDTO>();
    var Total = 0.0;
    var valorDescuento = 0.0;

    // 5. PROCESAR RESPUESTA DEL DESCUENTO (404 => sin descuento)
    DiscountDTO? discountDto = null;
    if (discountResponse.IsSuccessStatusCode)
    {
        discountDto = await discountResponse.Content.ReadFromJsonAsync<DiscountDTO>();
        valorDescuento = eventDto!.PrecioBase * request.Tickets * discountDto!.Porcentaje;
        Total = eventDto!.PrecioBase * request.Tickets - valorDescuento;
    }
    else if (discountResponse.StatusCode == HttpStatusCode.NotFound)
    {
        // No existe el código de descuento: continuar sin descuento
        Console.WriteLine($"Info: código de descuento '{request.DiscountCode}' no encontrado. Continuando sin descuento.");
        discountDto = null;
    }
    else
    {
        // Error inesperado al obtener descuento: registrar y continuar sin descuento
        Console.WriteLine($"Advertencia: al consultar descuento se recibió {discountResponse.StatusCode}");
        discountDto = null;
    }

    // 6. ACCIÓN: RESERVAR SILLAS (Inicio de SAGA)
    var reserveResponse = await eventClient.PostAsJsonAsync("/api/events/reserve",
        new { request.EventId, Quantity = request.Tickets });

    if (!reserveResponse.IsSuccessStatusCode)
        return Results.BadRequest("No hay sillas suficientes o el evento no existe.");

    try
    {   // 7. SIMULACIÓN DE PAGO (Punto Crítico)
        bool paymentSuccess = new Random().Next(1, 10) > 5;
        if (!paymentSuccess) 
            throw new Exception("Fondos insuficientes en la tarjeta de crédito.");

        var sillasRestantes = eventDto!.SillasDisponibles - request.Tickets;

        var eventDtoActualizado = eventDto with { SillasDisponibles = sillasRestantes };

        return Results.Ok(new { Status = "Éxito", Message = "Haz pagado $" + Total + 
            " exitosamente, disfruta del evento!", Event = eventDtoActualizado, Discount = discountDto });
    }
    catch (Exception ex)
    {
        // 8. COMPENSACIÓN con SAGA
        Console.WriteLine($"Error en pago: {ex.Message}. Liberando sillas...");
        await eventClient.PostAsJsonAsync("/api/events/release",
            new { request.EventId, Quantity = request.Tickets });
        return Results.Problem("Tu pago fue rechazado. No te preocupes, no te cobramos y tus sillas fueron liberadas.");
    }
});
app.MapControllers();
app.Run();

public record BookingRequest(int EventId, int Tickets, string DiscountCode);
