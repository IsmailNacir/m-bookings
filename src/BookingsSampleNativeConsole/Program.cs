using System;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.Bookings.Client;
using Microsoft.OData.Client;
using System.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Globalization;

namespace BookingsSampleNativeConsole
{
    public class Program
    {
        // See README.MD for instructions on how to get your own values for these two settings.
        // See also https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-authentication-scenarios#native-application-to-web-api
        private static string clientApplicationAppId;
        private static string tenantId;
        public static DateTime selectedCreneau;


        public static void Main()
        {
            try
            {
                var config = GetConfiguration();
                tenantId = config["Bookings_TenantID"];
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    Console.WriteLine("Update sample to include your own Tenant ID");
                    return;
                }

                clientApplicationAppId = config["Bookings_ClientID"];
                if (string.IsNullOrWhiteSpace(clientApplicationAppId))
                {
                    Console.WriteLine("Update sample to include your own client application ID");
                    return;
                }

                var clientApplication = PublicClientApplicationBuilder.Create(clientApplicationAppId)
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(tenantId)
                .Build();

                SecureString password = new NetworkCredential("", config["Password"]).SecurePassword;


                var authenticationResult = clientApplication.AcquireTokenByUsernamePassword(
                                    new[] { "Bookings.Read.All" },
                                    config["Email"], password).ExecuteAsync().Result;

                var graphService = new GraphService(
                    GraphService.ServiceRoot,
                    () => authenticationResult.CreateAuthorizationHeader());


                // Get the list of booking businesses that the logged on user can see.
                // NOTE: I'm not using 'async' in this sample for simplicity;
                // the ODATA client library has full support for async invocations.
                var bookingBusinesses = graphService.BookingBusinesses.ToArray();
                var bookingBusiness = bookingBusinesses.FirstOrDefault(b => b.DisplayName == "Support Mobile");

                // Play with the newly minted booking business
                var business = graphService.BookingBusinesses.ByKey(bookingBusiness.Id);

                // Add an external staff member (these are easy, as we don't need to find another user in the AD).
                // For an internal staff member, the application might query the user or the Graph to find other users.
                //affichage des 5 prochains jours ouvrés
                var c = 0;
                string[] days = new string[5];

                for (int i = 0; i < 7; i++)
                {
                    var day = DateTime.Today.AddDays(i).ToString("dddd");

                    if (day != "Saturday" && day != "Sunday")
                    {
                        Console.WriteLine($"({c + 1}) {day}");
                        days[c] = day;
                        c++;
                    }

                    if (c == 5) break;
                }

                Console.Write("Veuillez choisir un jour : ");
                string choiceNumber = Console.ReadLine();
                string selectedDay = days[int.Parse(choiceNumber) - 1];
                int defaultServiceDuration = (int)business.Services.First().DefaultDuration.TotalMinutes;
                //DateTime[] crenaux = new DateTime[30]; // a refaire
                List<DateTime> crenaux = new List<DateTime>();

                var staffs = business.StaffMembers.ToArray();
                for (int i = 0; i < staffs.Length; i++)
                {
                    var staffMemberWH = staffs[i].WorkingHours.ToList();
                    foreach (var _ in staffMemberWH)
                    {
                        if (_.TimeSlots.Count > 0)
                        {
                            Console.WriteLine(_.Day);
                            if (_.Day.ToString() == selectedDay)
                            {
                                int ct = 1;
                                Console.WriteLine("Veuillez choisir un creneau");
                                for (int j = 0; j < _.TimeSlots.Count; j++)
                                {

                                    var sTime = _.TimeSlots[j].Start.ToString();
                                    var eTime = _.TimeSlots[j].End.ToString();
                                    DateTime startTime = DateTime.Parse(sTime);
                                    DateTime endTime = DateTime.Parse(eTime);
                                    DateTime creneau = startTime;


                                    do
                                    {
                                        creneau = creneau.AddMinutes(defaultServiceDuration);
                                        Console.WriteLine($"({ct}) {creneau.TimeOfDay}");
                                        crenaux.Add(creneau);
                                        ct++;

                                    } while (creneau.TimeOfDay < endTime.TimeOfDay);


                                }

                                Console.Write("Votre crenaux :");
                                int indexOfCreneau =int.Parse(Console.ReadLine());
                                
                                foreach (var cr in crenaux)
                                {
                                    var index = crenaux.IndexOf(cr);
                                    if(index+1 == indexOfCreneau)
                                    {
                                        selectedCreneau = cr;
                                    }
                                }

                                // creat an appointement
                                Console.Write("Donnez votre Email :");
                                string email = Console.ReadLine();

                                Console.Write("Donnez votre Nom :");
                                string name = Console.ReadLine();

                                
                                var newAppointment = business.Appointments.NewEntityWithChangeTracking();
                                newAppointment.CustomerEmailAddress = email;
                                newAppointment.CustomerName = name;
                                newAppointment.ServiceId = business.Services.First().Id; // assuming we didn't deleted all services; we might want to double check first like we did with staff.
                                newAppointment.StaffMemberIds.Add(staffs[2].Id);
                                newAppointment.Reminders.Add(new BookingReminder { Message = "Hello", Offset = TimeSpan.FromHours(1), Recipients = BookingReminderRecipients.AllAttendees });
                                //var start = DateTime.Today.AddDays(1).AddHours(13).ToUniversalTime();
                                var start = selectedCreneau.ToUniversalTime();
                                var end = start.AddMinutes(defaultServiceDuration);
                                newAppointment.Start = new DateTimeTimeZone { DateTime = start.ToString("o"), TimeZone = "UTC" };
                                newAppointment.End = new DateTimeTimeZone { DateTime = end.ToString("o"), TimeZone = "UTC" };
                                Console.WriteLine("Creating appointment...");
                                graphService.SaveChanges(SaveChangesOptions.PostOnlySetProperties);
                                Console.WriteLine("Appointment created.");


                            }
                            else
                            {
                                Console.WriteLine("Aucun de nos agent n'est disponible dans ce jour, veuillez choisir un autre !");
                            }
                        }

                    }

                    Console.WriteLine("");

                }

                // In order for customers to interact with the booking business we need to publish its public page.
                // We can also Unpublish() to hide it from customers, but where is the fun in that?
                Console.WriteLine("Publishing booking business public page...");
                business.Publish().Execute();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Done. Press any key to exit.");
            Console.ReadKey();
        }

        private static IConfiguration GetConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true)
                .AddEnvironmentVariables();

            return builder.Build();
        }
    }
}
