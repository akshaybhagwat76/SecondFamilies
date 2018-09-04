using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace SecondFamilies.Models.AccountViewModels
{
    public class DonateGoodsViewModel
    {

        [Required]
        [EmailAddress]
        [Display(Name = "Email : *")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password : *")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password : *")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "First Name : *")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Donation Amount: *")]
        public string Amount { get; set; }


        [Display(Name = "Allocation")]
        public string Allocation { get; set; }


        [Display(Name = "Item : *")]
        public string Item { get; set; }

        [Display(Name = "Quantity : *")]
        public string Quantity { get; set; }

        [Display(Name = "Upload a photo of item")]
        public string ImageFile { get; set; }

        [Display(Name = "Do you need a pickup?")]
        public string NeedPickup { get; set; }

        [Display(Name = "Can you drop off?")]
        public string CanDropOff { get; set; }

        [Display(Name = "Available date/time for pickup/drop off : (dd/MM/yyyy")]
        public string DatePickDrop { get; set; }

        [Display(Name = "Donation Type")]
        public string DonationType { get; set; }
    }
}
