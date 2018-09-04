using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace SecondFamilies.Models
{
    public class Donate
    {
        public int id { get; set; }

        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Amount { get; set; }
        public string Allocation { get; set; }

        public string Item { get; set; }
        public string Quantity { get; set; }
        public string ImageUrl { get; set; }
        public string NeedPickup { get; set; }
        public string CanDropOff { get; set; }
        public string DatePickDrop { get; set; }
        public string DonationType { get; set; }
        public string DonationStatus { get; set; }

        public string AmazonWishList { get; set; }
    }
}
