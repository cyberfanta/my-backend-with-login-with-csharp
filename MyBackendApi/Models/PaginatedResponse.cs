using System.Collections.Generic;

namespace MyBackendApi.Models
{
    public class PaginatedResponse<T>
    {
        public IEnumerable<T> Data { get; set; } = default!;
        public PaginationMetadata Pagination { get; set; } = default!;
    }

    public class PaginationMetadata
    {
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ItemsPerPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
} 