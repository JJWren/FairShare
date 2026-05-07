using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
namespace AppShared.ViewModels
{
    public sealed class ErrorViewModel
    {
        public int StatusCode { get; set; }

        public string? Title { get; set; }

        public string? Message { get; set; }

        public string? TraceId { get; set; }

        public string? Detail { get; set; }
    }
}




