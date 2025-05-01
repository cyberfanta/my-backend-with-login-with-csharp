# Proyecto Backend con NestJS, PostgreSQL y Docker

## Resumen del Desarrollo

El proyecto consiste en un backend desarrollado con NestJS, PostgreSQL y Docker con funcionalidades de autenticación.

### 1. Configuración Inicial y Sistema de Login

- Se corrigieron errores de importación en `main.ts` añadiendo las importaciones faltantes de NestFactory y AppModule
- Se crearon endpoints de autenticación (signup, login, logout, delete-account)
- Se implementó autenticación con tokens JWT Bearer
- Se resolvieron problemas de conexión entre pgAdmin y PostgreSQL en Docker

### 2. Integración con PgAdmin

- Se añadió el contenedor de pgAdmin a la configuración de Docker
- Se solucionaron problemas de conexión utilizando nombres de contenedores en lugar de localhost
- Se creó configuración de red en Docker Compose

### 3. Mejoras de API

- Se movieron los endpoints relacionados con autenticación del grupo 'admin' al grupo 'user'
- Se implementó un endpoint para refrescar tokens
- Se añadió validación de unicidad para nombres de usuario durante el registro
- Se mejoró el manejo de errores con códigos HTTP específicos y mensajes de error claros

### 4. Implementación de Paginación

- Se creó un endpoint para listar usuarios con paginación
- Se implementaron metadatos para la paginación (totalItems, currentPage, totalPages)
- Se añadieron indicadores de ayuda (hasNextPage, hasPreviousPage)
- Se mejoraron los nombres de campos y la documentación

### 5. Mejoras en Entidades

- Se actualizó la entidad User con campos adicionales (name, lastname)
- Se cambió el campo ID a tipo UUID
- Se añadieron marcas de tiempo (createdAt, updatedAt)
- Se modificó la ordenación para ser por createdAt descendente (más recientes primero)

### 6. Documentación

- Se documentaron todos los endpoints con Swagger
- Se añadieron respuestas detalladas de error
- Se mejoraron las descripciones de parámetros y valores de ejemplo

## Código Fuente Relevante

### Entidad Usuario (user.entity.ts)

```typescript
import { Entity, Column, PrimaryGeneratedColumn, CreateDateColumn, UpdateDateColumn } from 'typeorm';

@Entity()
export class User {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ unique: true })
  username: string;

  @Column()
  password: string;

  @Column({ nullable: true })
  name?: string;

  @Column({ nullable: true })
  lastname?: string;

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
```

### Servicio de Usuario (user.service.ts)

```typescript
import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { User } from '../user.entity';

export interface PaginatedUsers {
  data: User[];
  pagination: {
    totalItems: number;
    currentPage: number;
    totalPages: number;
    itemsPerPage: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
  };
}

@Injectable()
export class UserService {
  constructor(
    @InjectRepository(User)
    private userRepository: Repository<User>,
  ) {}

  async findAllUsers(pageNumber: number = 1, itemsPerPage: number = 10): Promise<PaginatedUsers> {
    // Convert parameters to numbers to ensure consistency
    const page = Number(pageNumber) || 1;
    const limit = Number(itemsPerPage) || 10;
    
    // Ensure values are within acceptable ranges
    const safePage = page > 0 ? page : 1;
    const safeLimit = limit > 0 && limit <= 100 ? limit : 10;
    
    // Calculate offset for database query
    const offset = (safePage - 1) * safeLimit;
    
    // Execute query with pagination
    const [data, totalItems] = await this.userRepository.findAndCount({
      select: ['id', 'username', 'name', 'lastname', 'createdAt', 'updatedAt'], // Include timestamps
      skip: offset,
      take: safeLimit,
      order: { createdAt: 'DESC' }, // Order by createdAt (newest first)
    });
    
    // Calculate total pages (minimum is 1 page, even for empty results)
    const totalPages = Math.max(1, Math.ceil(totalItems / safeLimit));
    
    // Determine if there are next/previous pages
    // - Next page exists if current page is less than total pages
    // - Previous page exists if current page is greater than 1
    // - No pages exist if there are no items (both will be false)
    const hasNextPage = totalItems > 0 && safePage < totalPages;
    const hasPreviousPage = totalItems > 0 && safePage > 1;
    
    // Return paginated result with improved metadata
    return {
      data,
      pagination: {
        totalItems,
        currentPage: safePage,
        totalPages,
        itemsPerPage: safeLimit,
        hasNextPage,
        hasPreviousPage
      },
    };
  }
} 
```

### Controlador de Usuario (user.controller.ts)

```typescript
import { Controller, Get, Headers, Query, UnauthorizedException } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse, ApiBearerAuth, ApiHeader, ApiQuery } from '@nestjs/swagger';
import { UserService } from './user.service';
import { AuthService } from '../auth/auth.service';

@ApiTags('user')
@Controller('user')
export class UserController {
  constructor(
    private readonly userService: UserService,
    private readonly authService: AuthService,
  ) {}

  @Get('users')
  @ApiOperation({
    summary: 'List all users',
    description: 'Returns a paginated list of all users in the system. Requires authentication. Users are ordered by createdAt (newest first).'
  })
  @ApiBearerAuth('JWT-auth')
  @ApiHeader({
    name: 'Authorization',
    description: 'JWT token with format: Bearer [token]',
    required: true,
    schema: {
      type: 'string',
      example: 'Bearer eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9...'
    }
  })
  @ApiQuery({
    name: 'pageNumber',
    required: false,
    description: 'Page number to retrieve (starting from 1)',
    type: Number,
    example: 1
  })
  @ApiQuery({
    name: 'itemsPerPage',
    required: false,
    description: 'Number of items to display per page (default: 10, max: 100)',
    type: Number,
    example: 10
  })
  @ApiResponse({
    status: 200,
    description: 'Returns a paginated list of users',
    schema: {
      type: 'object',
      properties: {
        data: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              id: { type: 'string', example: '550e8400-e29b-41d4-a716-446655440000' },
              username: { type: 'string', example: 'johndoe' },
              name: { type: 'string', example: 'John' },
              lastname: { type: 'string', example: 'Doe' },
              createdAt: { type: 'string', format: 'date-time', example: '2023-05-20T14:56:29.000Z' },
              updatedAt: { type: 'string', format: 'date-time', example: '2023-05-20T15:32:12.000Z' }
            }
          }
        },
        pagination: {
          type: 'object',
          properties: {
            totalItems: { type: 'number', example: 50, description: 'Total number of items in the database' },
            currentPage: { type: 'number', example: 1, description: 'Current page being displayed' },
            totalPages: { type: 'number', example: 5, description: 'Total number of pages available' },
            itemsPerPage: { type: 'number', example: 10, description: 'Number of items displayed per page' },
            hasNextPage: { type: 'boolean', example: true, description: 'Whether there is a next page available' },
            hasPreviousPage: { type: 'boolean', example: false, description: 'Whether there is a previous page available' }
          }
        }
      }
    }
  })
  @ApiResponse({ status: 401, description: 'Unauthorized - Invalid or expired token' })
  async getUsers(
    @Headers('Authorization') authHeader: string,
    @Query('pageNumber') pageNumber?: number,
    @Query('itemsPerPage') itemsPerPage?: number,
  ) {
    // Validate token
    const token = authHeader?.split(' ')[1];
    if (!token) throw new UnauthorizedException('No token provided');
    
    // Verify token is valid
    await this.authService.verifyToken(token);
    
    // Fetch and return users with pagination
    return this.userService.findAllUsers(pageNumber, itemsPerPage);
  }
} 
```

## Cambios Notables

- Se eliminaron los archivos del módulo `admin` ya que su funcionalidad se trasladó al módulo `user`:
  - src/admin/admin.module.ts
  - src/admin/admin.service.ts
  - src/admin/admin.controller.ts

## Próximos Pasos Recomendados

1. Implementar pruebas unitarias y de integración para los endpoints
2. Añadir roles y permisos al sistema de usuario
3. Implementar verificación por email para nuevos registros
4. Mejorar la seguridad con rate limiting y protección contra ataques comunes
5. Configurar CI/CD para despliegue automático 