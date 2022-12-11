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
using Microsoft.OData.Edm;

namespace BookingsSampleNativeConsole
{
    public class Program
    {
        // See README.MD for instructions on how to get your own values for these two settings.
        // See also https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-authentication-scenarios#native-application-to-web-api
        private static string clientApplicationAppId;
        private static string tenantId;
        public static DateTime selectedCreneau;

        private static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == System.DayOfWeek.Saturday
                || date.DayOfWeek == System.DayOfWeek.Sunday;
        }

        private static DateTime GetNextWorkingDay(DateTime date)
        {
            do
            {
                date = date.AddDays(1);
            } while (IsWeekend(date));

            return date;
        }

        //Delete Function
        //private bool DeleteApp(int appId)
        //{
        //    business.Appointments.
        //    return true;
        //}

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
                //var c = 0;
                string[] days = new string[5];
                DateTime[] wokingDays = new DateTime[5];



                /*for (int i = 0; i < 7; i++)
                {
                    var day = DateTime.Today.AddDays(i).ToString("dddd");


                    if (day != "Saturday" && day != "Sunday")
                    {
                        Console.WriteLine($"({c + 1}) {day}");
                        days[c] = day;

                        c++;
                    }

                    if (c == 5) break;
                }*/

                var dt = GetNextWorkingDay(DateTime.Now.Date);

                for (int i = 0; i < 5; i++)
                {
                    Console.WriteLine($"({i + 1}) {dt.DayOfWeek}");
                    //Console.WriteLine(dt.ToString("dd/MM/yyyy"));
                    wokingDays[i] = (dt);
                    dt = GetNextWorkingDay(dt);
                }

                Console.Write("Veuillez choisir un jour : ");
                string choiceNumber = Console.ReadLine();
                DateTime selectedDay = wokingDays[int.Parse(choiceNumber) - 1];
                int defaultServiceDuration = (int)business.Services.First().DefaultDuration.TotalMinutes;
                List<DateTime> crenaux = new List<DateTime>();

                var staffs = business.StaffMembers.ToArray();
                var appointements = business.Appointments.ToArray();
                List<DateTime> startApps = new List<DateTime>();

                for (int i = 0; i < staffs.Length; i++)
                {
                    var staffApps = appointements.Where(ap => ap.StaffMemberIds.Contains(staffs[i].Id));

                    foreach (var sApp in staffApps)
                    {
                        startApps.Add(DateTime.Parse(sApp.Start.DateTime.ToString()));
                    }

                    var staffMemberWH = staffs[i].WorkingHours.ToList();

                    foreach (var wh in staffMemberWH)
                    {
                        if (wh.TimeSlots.Count > 0)
                        {
                            if (wh.Day.ToString() != selectedDay.DayOfWeek.ToString())
                            {
                                Console.WriteLine("Aucun de nos agent n'est disponible dans ce jour, veuillez choisir un autre !");
                            }

                            else
                            {
                                for (int j = 0; j < wh.TimeSlots.Count; j++)
                                {
                                    var sTime = wh.TimeSlots[j].Start;
                                    var eTime = wh.TimeSlots[j].End;


                                    var startTime = new DateTime(selectedDay.Year, selectedDay.Month, selectedDay.Day, sTime.Hours, sTime.Minutes, sTime.Seconds);
                                    var endTime = new DateTime(selectedDay.Year, selectedDay.Month, selectedDay.Day, eTime.Hours, eTime.Minutes, eTime.Seconds);


                                    DateTime creneau = startTime;

                                    do
                                    {
                                        if (!startApps.Contains(creneau))
                                        {
                                            crenaux.Add(creneau);
                                        }

                                        creneau = creneau.AddMinutes(defaultServiceDuration);

                                    } while (creneau.AddMinutes(defaultServiceDuration) <= endTime);

                                }

                            }
                        }
                    }
                }

                int ct = 1;
                var distinctCreneau = crenaux.Distinct().ToList();

                Console.WriteLine("Veuillez choisir un creneau :");

                foreach (var creneau in distinctCreneau)
                {
                    Console.WriteLine($"({ct}) {creneau.TimeOfDay}");

                    ct++;
                }

                Console.Write("Votre crenaux :");
                int indexOfCreneau = int.Parse(Console.ReadLine());

                foreach (var cr in distinctCreneau)
                {
                    var index = distinctCreneau.IndexOf(cr);

                    if (index + 1 == indexOfCreneau)
                    {
                        selectedCreneau = cr;
                    }
                }

                //Vote Staff
                var staffId = "";

                for (int i = 0; i < staffs.Length; i++)
                {
                    var staffApps = appointements.Where(ap => ap.StaffMemberIds.Contains(staffs[i].Id));
                    foreach (var sApp in staffApps)
                    {
                        startApps.Add(DateTime.Parse(sApp.Start.DateTime.ToString()));
                    }

                    if (!startApps.Contains(selectedCreneau))
                    {
                        staffId = staffs[i].Id;
                        break;
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
                newAppointment.StaffMemberIds.Add(staffId);
                newAppointment.Reminders.Add(new BookingReminder { Message = "Hello", Offset = TimeSpan.FromHours(1), Recipients = BookingReminderRecipients.AllAttendees });
                DateTime start = new DateTime(selectedDay.Year, selectedDay.Month, selectedDay.Day, selectedCreneau.Hour, selectedCreneau.Minute, 00).ToUniversalTime();

                var end = start.AddMinutes(defaultServiceDuration);
                newAppointment.Start = new DateTimeTimeZone { DateTime = start.ToString("o"), TimeZone = "UTC" };
                newAppointment.End = new DateTimeTimeZone { DateTime = end.ToString("o"), TimeZone = "UTC" };
                Console.WriteLine("Creating appointment...");
                graphService.SaveChanges(SaveChangesOptions.PostOnlySetProperties);
                Console.WriteLine("Appointment created.");
                Console.WriteLine("");

                Console.WriteLine("Voici votre RDV");
                foreach (var appointment in business.Appointments.GetAllPages().Where(x => x.Id == newAppointment.Id))
                {
                    
                    Console.WriteLine($"{DateTime.Parse(appointment.Start.DateTime).ToLocalTime()}: {appointment.ServiceName} with {appointment.CustomerName}");
                }
                Console.Write("Entrez X si vous voulez supprimer votre RDV : ");
                string wantToDelet = Console.ReadLine();
                if(wantToDelet == "X")
                {
                    var allApp = business.Appointments.GetAllPages();
                    allApp = allApp.Where(x => x.Id != newAppointment.Id);
                    graphService.SaveChanges(SaveChangesOptions.PostOnlySetProperties);
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
