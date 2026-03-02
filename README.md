# Hotel Booking API

Production-ready RESTful API built with ASP.NET Core using Clean Architecture.

## Features
- JWT Authentication
- Role-Based Authorization (Admin/User)
- CRUD: Hotels, Rooms, Bookings
- Pagination & Filtering
- Global Exception Handling
- Swagger/OpenAPI Docs
- Structured Logging

## Architecture
- **Domain**: core entities + rules  
- **Application**: DTOs, validation, interfaces  
- **Infrastructure**: EF Core + database access  
- **API**: endpoints, middleware, auth  

## Tech Stack
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- AutoMapper
- FluentValidation
- Serilog

## Run Locally
```bash
git clone https://github.com/prince-tiwaa/hotel-booking-api.git
cd hotel-booking-api
dotnet restore
dotnet ef database update
dotnet run
