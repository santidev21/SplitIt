using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Application.DTOs
{
    public class RegisterPaymentDto
    {
        public int PayerUserId { get; set; }
        public int GroupId { get; set; }
        public decimal Amount { get; set; }
    }
}
