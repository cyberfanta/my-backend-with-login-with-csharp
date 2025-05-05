# Backend con .NET Core, SQL Server y Docker con Autenticación

Este proyecto implementa un backend con .NET Core 7.0, SQL Server y Docker que incluye un sistema completo de autenticación.

## Características principales

- Autenticación con JWT Token
- Endpoints protegidos con Bearer Authentication
- SQL Server en Docker
- Adminer como administrador de base de datos
- Documentación con Swagger

## Endpoints

### Autenticación
- `POST /Auth/signup` - Registro de usuarios
- `POST /Auth/login` - Inicio de sesión
- `POST /Auth/refresh-token` - Renovación de token
- `POST /Auth/logout` - Cierre de sesión
- `DELETE /Auth/delete-account` - Eliminación de cuenta

### Usuarios
- `GET /User` - Listado de usuarios con paginación
- `GET /User/{id}` - Obtener usuario por ID

### Documentos
- `POST /Document/process-pdf` - Procesar un archivo PDF
- `POST /Document/process-pdfs` - Procesar múltiples archivos PDF
- `POST /Document/analyze-pdf` - Analizar un archivo PDF con IA 
- `POST /Document/analyze-pdfs` - Analizar múltiples archivos PDF con IA

## Configuración con Docker

El proyecto incluye un archivo `docker-compose.yml` que configura:

- API en el puerto 4500
- SQL Server en el puerto 4502
- Adminer en el puerto 4501

## Cómo ejecutar

```bash
docker-compose up --build -d
```

La API estará disponible en http://localhost:4500/api

## Prerequisites

- Docker and Docker Compose

## Project Structure

```
my-backend-with-login-with-csharp/
├── MyBackendApi/               # Main project
│   ├── Controllers/            # API Controllers
│   ├── Data/                   # Database configuration
│   ├── Models/                 # Models and DTOs
│   └── Services/               # Application services
├── docker-compose.yml          # Docker Compose configuration
└── Dockerfile                  # Dockerfile for building the image
```

## SQL Server Credentials

- Server: localhost,4502 (from host) or sqlserver,1433 (from other containers)
- User: sa
- Password: Password123!
- Database: MyBackendDb

## Ports 

- API Backend: 4500
- Swagger UI: 4500/api
- Adminer (DB Manager): 4501
- SQL Server: 4502
- Azure SQL Edge: 4503

## Database Manager (Adminer)

To manage the database, you can use Adminer included as part of the Docker containers:
1. Access http://localhost:4501
2. Select "SQL Server" as system
3. Server: sqlserver
4. User: sa
5. Password: Password123!
6. Database: MyBackendDb

## License

MIT 