using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class RegisterPaymentDto
    {
        [Range(1, int.MaxValue)]
        public int PayerUserId { get; set; }

        [Range(1, int.MaxValue)]
        public int GroupId { get; set; }

        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }
    }
}
