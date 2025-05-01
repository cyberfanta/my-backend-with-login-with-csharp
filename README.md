# My Backend with Login (.NET Core)

This project is a backend developed with .NET Core, SQL Server, and Docker. It includes JWT authentication functionality and a REST API for user management.

## Features

- JWT Authentication
- RESTful API
- Pagination support
- SQL Server database
- Swagger documentation
- Docker for development and deployment

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

## Installation and Execution

1. Clone this repository
2. From the project root, run:

```bash
docker-compose up -d
```

3. Access the API at http://localhost:4500
4. Access Swagger documentation at http://localhost:4500/api
5. To connect to the database manager Adminer: http://localhost:4501

## Endpoints

### Authentication

- POST /auth/signup - Register a new user
- POST /auth/login - User login
- POST /auth/refresh-token - Refresh JWT token
- POST /auth/logout - Logout (requires authentication)
- DELETE /auth/delete-account - Delete account (requires authentication)

### Users

- GET /user - Get paginated list of users
- GET /user/{id} - Get user by ID

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