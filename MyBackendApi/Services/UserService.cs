using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyBackendApi.Data;
using MyBackendApi.Models;

namespace MyBackendApi.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedResponse<User>> GetUsers(int pageNumber = 1, int pageSize = 10)
        {
            // Asegurar valores válidos
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : (pageSize > 100 ? 100 : pageSize);

            // Calcular salto
            var skip = (pageNumber - 1) * pageSize;

            // Consultar datos
            var query = _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt);

            // Contar total de elementos
            var totalItems = await query.CountAsync();

            // Obtener página solicitada
            var data = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            // Calculamos valores de la paginación
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var hasNextPage = pageNumber < totalPages;
            var hasPreviousPage = pageNumber > 1;

            // Devolvemos resultado paginado
            return new PaginatedResponse<User>
            {
                Data = data,
                Pagination = new PaginationMetadata
                {
                    TotalItems = totalItems,
                    CurrentPage = pageNumber,
                    TotalPages = totalPages,
                    ItemsPerPage = pageSize,
                    HasNextPage = hasNextPage,
                    HasPreviousPage = hasPreviousPage
                }
            };
        }

        public async Task<User?> GetUserById(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }
    }
} 