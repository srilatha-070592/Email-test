using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EmailComponent.Controllers
{
    [EnableCors("CorsPolicy")]
    public class SendGridEmailController : Controller
    {
        private readonly IConfiguration _configuration;
        public SendGridEmailController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Author:Praveen Alwar
        /// This method sends the mail in synchronous and asynchronous format.
        /// The initiator will get the response in boolean format if the mail was sent succesfully
        /// please mention the SMTP relay settings in the configuration file of the calling application
        /// </summary>
        /// <param name="emailDetails"></param>
        /// <returns></returns>
        [HttpPost]
        [Microsoft.AspNetCore.Mvc.Route("api/orchestration/email")]
        public async Task<bool> SendEmail([FromBody] SendGridMessage data)
        {
          
            var msg = new SendGridMessage();

            bool result = false;

            try
            {
               var client = new SendGridClient(_configuration["SGapiKey"].ToString());

                data.ReplyTo.Email = data.ReplyTo.Email.TrimEnd(';');
                data.ReplyTo.Email = data.ReplyTo.Email.TrimEnd(',');



                //check if there are multiple email reciepients 
                // API Callers can send either comma or semi-colon as delimeter
                if (data.ReplyTo.Email.Contains(",") || (data.ReplyTo.Email.Contains(";")))
                {
                    
                    string[] emailto = Array.Empty<string>();
                    //split the email ids with comma separator
                    emailto = data.ReplyTo.Email.Split(',');
                    //split the email ids with semi-colon separator
                    if (emailto.Length <= 1)
                        emailto = data.ReplyTo.Email.Split(';');
                    //add the list of 'To-Email' Address to a list
                    var tosList = new List<EmailAddress>();

                    foreach (var i in emailto)
                    {
                        tosList.Add(new EmailAddress(i));
                    }

                    //Parameterize the SendGrid Message Object
                    msg = new SendGridMessage()
                    {

                        From = new EmailAddress(data.From.Email, data.From.Name),
                        Subject = data.Subject,
                        HtmlContent = data.Contents[0].Value,

                    };
                    //invoke the SendGrid API for email receipients
                    msg = MailHelper.CreateSingleEmailToMultipleRecipients(msg.From, tosList, msg.Subject, "", msg.HtmlContent, true);

                }
                //Code below when there is a single email receipient
                else
                {
                    msg = new SendGridMessage()
                    {

                        From = new EmailAddress(data.From.Email.Trim(), data.From.Name),
                        Subject = data.Subject,
                        HtmlContent = data.Contents[0].Value,
                        ReplyTo = new EmailAddress(data.ReplyTo.Email.Trim(), data.ReplyTo.Name)

                    };
                    msg.AddTo(new EmailAddress(data.ReplyTo.Email.Trim(), data.ReplyTo.Name));
                }
                //send the email message to SendGrid
               var Sendgridresult = await client.SendEmailAsync(msg);
                result = Sendgridresult.IsSuccessStatusCode;
                              
            }
                catch(Exception ex)
                {
                    var requestTelemetry = HttpContext.Features.Get<RequestTelemetry>();
                    var globalProperties = requestTelemetry?.Context.GlobalProperties;
                    if (globalProperties is not null)
                    {
                        globalProperties["component"] = "EmailComponent";
                        if (ex.InnerException != null)
                            globalProperties["exception_message"] = ex.InnerException.Message;
                        else
                            globalProperties["exception_message"] = ex.Message;
                    }

                    HttpClient _client = new HttpClient();
                    string errorlogURL = _configuration["AuditEndpoint"].ToString();
                    HttpContent content = new StringContent(ex.Message);
                    await _client.PostAsync(errorlogURL, content);
           

                }
            // A success status code means SendGrid received the email request and will process it.
            // Errors can still occur when SendGrid tries to send the email. 
            // If email is not received, use this URL to debug: https://app.sendgrid.com/email_activity 

            return result;
        }
        

        /// <summary>
        /// This method will use the given HTML template and replace the placeholders with  the given dynamic values
        /// </summary>
        /// <param name="_replaceVariables"></param>
        /// <param name="TemplatePath"></param>
        /// <returns></returns>
        private static string PopulateBody(Hashtable _replaceVariables, string TemplatePath)
        {

            string body = string.Empty;

            try
            {
                //readthrough the template and identify the dynamic variables
                using (StreamReader reader = new StreamReader(TemplatePath))
                {
                    body = reader.ReadToEnd();
                }

                foreach (DictionaryEntry de in _replaceVariables)
                {
                    body = body.Replace("{" + de.Key.ToString() + "}", de.Value.ToString());

                }

            }
            catch (Exception ex)
            {
                //commented this intentionally as we have the error bubbled in AZURE
                //AddtoLogFile("Error sending mail-Populate Body Exception", ex.InnerException.Message);
            }
            return body;
        }

    }
}
