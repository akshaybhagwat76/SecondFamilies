using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MimeKit;
using MailKit.Net.Smtp;
using System.IO;
using SecondFamilies.Models;
using SecondFamilies.Models.AccountViewModels;

namespace SecondFamilies.Services
{
    public interface IMessaging
    {
        Task SendDonationEmail(Donate donate);
    }
    public class Messaging : IMessaging
    {
        private readonly IHostingEnvironment env;

        public Messaging(IHostingEnvironment hostingEnvironment)
        {
            env = hostingEnvironment;
        }

        public async Task SendDonationEmail(Donate donateData)
        {
            try
            {
                //From Address  
                string FromAddress = "foodc10@gmail.com";
                string FromAdressTitle = "Email from Second Family";
                //To Address  
                string ToAddress = donateData.Email;
                string ToAdressTitle = "Second Family";
                string Subject = "Donate Goods & Items";
                string BodyContent = "";
                BodyContent += @"Hey " + donateData.FirstName + " " + donateData.LastName + ".";
                BodyContent += "Thanks for your Donation of Goods & Items.<br /><br />";
                BodyContent += "Item - " + donateData.Item + ".<br />";
                BodyContent += "Quantity - " + donateData.Quantity + ".<br />";
                BodyContent += "Location - " + donateData.Address + ".<br />";
                BodyContent += "Do you need a pickup? - " + donateData.NeedPickup + ".<br />";
                BodyContent += "Can you drop off? - " + donateData.CanDropOff + ".<br />";
                BodyContent += "Available date/time for pickup/drop off :- " + donateData.DatePickDrop + ".<br /><br /><br />";
                BodyContent += "Thank You";

                //Smtp Server  
                string SmtpServer = "smtp.gmail.com";
                //Smtp Port Number  
                int SmtpPortNumber = 587;

                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(FromAdressTitle, FromAddress));
                mimeMessage.To.Add(new MailboxAddress(ToAdressTitle, ToAddress));
                mimeMessage.Subject = Subject;

                var builder = new BodyBuilder();
                builder.HtmlBody = BodyContent;
                foreach (string file in Directory.EnumerateFiles(
                env.WebRootPath + "\\dimage\\",
                "*",
                SearchOption.AllDirectories)
                )
                {
                    builder.Attachments.Add(file);
                }
                mimeMessage.Body = builder.ToMessageBody();
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(SmtpServer, SmtpPortNumber, false).ConfigureAwait(false);
                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                    await client.AuthenticateAsync("foodc10@gmail.com", "foodc@619619")
                        .ConfigureAwait(false);
                    await client.SendAsync(mimeMessage).ConfigureAwait(false);
                    await client.DisconnectAsync(true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
