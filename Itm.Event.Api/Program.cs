using Itm.Event.Api.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//var jwtSettings = builder.Configuration.GetSection("JwtSettings");
//var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidIssuer = jwtSettings["Issuer"],
//            ValidateAudience = true,
//            ValidAudience = jwtSettings["Audience"],
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
//        };
//    });

//builder.Services.AddAuthorization();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

var eventDB = new List<EventDTO>
{
    new (1, "Concierto de Rock", 50.0, 100),
    new (2, "Concierto de Salsa", 30.0, 50),
    new (3, "Concierto hasta las 6 am", 40.0, 200)
};

// Endpoint consulta de eventos
app.MapGet("/api/events/{id}", (int id) =>
{
    var item = eventDB.FirstOrDefault(e => e.Id == id);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});

// Endpoint Reserve para reservar sillas para el evento
app.MapPost("/api/events/reserve", (ReserveDTO request) =>
{
    var item = eventDB.FirstOrDefault(c => c.Id == request.EventId);
    if (item is null)
        return Results.NotFound(new { Error = "Esta silla no esta disponible" });
    
    if (item.SillasDisponibles < request.Quantity)
        return Results.BadRequest(new { Error = "No hay sillas suficientes", CurrentStock = item.SillasDisponibles });
    
    var index = eventDB.IndexOf(item);
    eventDB[index] = item with { SillasDisponibles = item.SillasDisponibles - request.Quantity };
    return Results.Ok(new { Message = "Disponibilidad actualizada", NewStock = eventDB[index].SillasDisponibles });
});

// Enpoint Release para cancelar la reserva
app.MapPost("/api/events/release", (ReserveDTO request) =>
{
    var item = eventDB.FirstOrDefault(c => c.Id == request.EventId);
    if (item is null) return Results.NotFound();

    var index = eventDB.IndexOf(item);
    eventDB[index] = item with { SillasDisponibles = item.SillasDisponibles + request.Quantity };
    Console.WriteLine($"Se restauraron {request.Quantity} " +
        $"sillas para el evento {item.Nombre}. Nueva disponibilidad: {eventDB[index].SillasDisponibles}");
    return Results.Ok(new { Message = "Disponibilidad actualizada"
        , NewStock = eventDB[index].SillasDisponibles });
});

app.MapControllers();
app.Run();