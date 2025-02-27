﻿using DanpheEMR.DalLayer;
using DanpheEMR.Enums;
using DanpheEMR.ServerModel.PatientModels;
using DanpheEMR.ServerModel.SSFModels;
using DanpheEMR.ServerModel.SSFModels.ClaimResponse;
using DanpheEMR.ServerModel.SSFModels.SSFResponse;
using DanpheEMR.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DanpheEMR.Services.SSF
{
    public class SSFService : ISSFService
    {
        public async Task<SSFPatientDetails> GetPatientDetails(SSFDbContext ssfDbContext, string patientNo)
        {
            var pdetails = new SSFPatientDetails();
            using (var client = new HttpClient())
            {
                string patientImagePath = "";
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var response = client.GetAsync($"Patient/?identifier={patientNo}").Result;
                if (response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<ServerModel.SSFModels.SSFResponse.Root>(details);
                    if (data.entry != null)
                    {
                        foreach (var dt in data.entry)
                        {
                            if (dt.resource.address != null && dt.resource.address.Count > 0)
                            {
                                foreach (var ad in dt.resource.address)
                                {
                                    pdetails.Address = ad.text;
                                }
                            }

                            pdetails.birthdate = dt.resource.birthDate;
                            pdetails.gender = dt.resource.gender;
                            pdetails.UUID = dt.resource.id;
                            if (dt.resource.name != null && dt.resource.name.Count > 0)
                            {
                                foreach (var na in dt.resource.name)
                                {
                                    pdetails.family = na.family;
                                    pdetails.name = na.given[0];
                                }
                            }

                            //foreach (var item in dt.resource.identifier)
                            //{
                            //    pdetails.UUID = item.value;
                            //}
                            if (dt.resource.photo != null && dt.resource.photo.Count > 0)
                            {
                                /*foreach (var item in dt.resource.photo)
                                {
                                    pdetails.img = item.url;
                                }*/
                                patientImagePath = dt.resource.photo[0].url.Replace("\\", "/");
                            }

                        }
                    }
                    var employerList = await GetSsfEmployerList(ssfDbContext, pdetails.UUID);
                    pdetails.ssfEmployerList = employerList;
                    if (patientImagePath != null)
                    {
                        pdetails.img = GetSsfPatientPhoto(client, patientImagePath);
                    }
                }
                return pdetails;
            }

        }

        private string GetSsfPatientPhoto(HttpClient client, string img)
        {
            var response = client.GetAsync(img).Result;
            string base64Image = "";
            if (response.IsSuccessStatusCode)
            {
                byte[] imageBytes = response.Content.ReadAsByteArrayAsync().Result;
                base64Image = Convert.ToBase64String(imageBytes);
            }
            return base64Image;
        }

        public async Task<List<EligibilityResponse>> GetElegibility(SSFDbContext ssfDbContext, string patientNo, string visitDate)
        {
            var eligibilityResponse = new List<EligibilityResponse>();
            var request = new EligibilityRequest();
            var patient = new EligibilityPatientData();
            var listExtension = new List<EligibilityExtension>();
            var extreq = new EligibilityExtension();
            extreq.url = "visitDate";
            extreq.valueString = visitDate;
            listExtension.Add(extreq);
            try
            {
                patient.reference = $"Patient/{patientNo}";
                request.patient = patient;
                request.resourceType = "CoverageEligibilityRequest";
                request.extension = listExtension;
                var client = new HttpClient();
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var jsonContent = JsonConvert.SerializeObject(request);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"CoverageEligibilityRequest/", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<EligibilityRoot>(result);
                    if (data != null)
                    {
                        eligibilityResponse = ParseSSFEligibilityResponse(data, ssfDbContext);
                    }

                }
                return eligibilityResponse;
            }
            catch (Exception ee)
            {
                throw ee;
            }
        }


        private static List<EligibilityResponse> ParseSSFEligibilityResponse(EligibilityRoot data, SSFDbContext ssfDbContext)
        {
            var elegibilityResponse = new List<EligibilityResponse>();
            decimal allowMoney = 0;
            decimal usedMoney = 0;
            var extension = new List<EligibilityExtension>();
            var insurance = data.insurance;

            var medicalSchemeName = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SsfSearchStringForSchemeNames", "Medical"); //Krishna, 27thFeb'23 this is hardcoded value
            var accidentalSchemeName = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SsfSearchStringForSchemeNames", "Accidental"); //Krishna, 27thFeb'23 this is hardcoded value

            //Krishna, 27thFeb'23 Below logic is used to find the index of Medical/Accidental Schemes coming from SSF Server
            var indexForAccident = insurance.FindIndex(ins =>
                ins.extension.Any(e => e.valueString.ToString().Like(accidentalSchemeName))
            );

            var indexForMedical = insurance.FindIndex(ins =>
                ins.extension.Any(e => e.valueString.ToString().Like(medicalSchemeName))
            );

            //Krishna, 27thFeb'23 throw exception in case of issue like index not found.
            if (indexForMedical <= -1 || indexForAccident <= -1)
            {
                throw new Exception("Scheme Search KeyWord not matched.");
            }

            //accident and other information 
            var accident = insurance[indexForAccident];
            var accidentext = accident.extension[0];
            var accidentitem = accident.item[0];
            var accbenifit = accidentitem.benefit;
            foreach (var acc in accbenifit)
            {
                usedMoney = acc.usedMoney.value;
                allowMoney = acc.allowedMoney.value;
            }
            elegibilityResponse.Add(new EligibilityResponse
            {
                Inforce = accident.inforce,
                SsfSchemeName = accidentext.valueString.ToString(),
                AccidentBalance = allowMoney,
                UsedMoney = usedMoney,
                OpdBalance = 0,
                IPBalance = 0,
                SsfEligibilityType = ENUM_SSF_EligibilityType.Accident
            });

            //Medical and other information
            var medicaldata = insurance[indexForMedical];
            var medicaltext = medicaldata.extension[0];
            var medicalOP = medicaldata.extension[1];
            var medicalIP = medicaldata.extension[2];
            var medicalitemitem = medicaldata.item[0];
            var medicalbenifit = medicalitemitem.benefit;
            foreach (var acc in medicalbenifit)
            {
                usedMoney = acc.usedMoney.value;
                allowMoney = acc.allowedMoney.value;
            }
            elegibilityResponse.Add(new EligibilityResponse
            {
                Inforce = medicaldata.inforce,
                SsfSchemeName = medicaltext.valueString.ToString(),
                AccidentBalance = 0,
                UsedMoney = usedMoney,
                OpdBalance = Convert.ToDecimal(medicalOP.valueString),
                IPBalance = Convert.ToDecimal(medicalIP.valueString),
                SsfEligibilityType = ENUM_SSF_EligibilityType.Medical
            }); ;
            return elegibilityResponse;
        }

        public async Task<List<List<Company>>> GetEmployerList(SSFDbContext ssfDbContext, string SSFPatientUUID)
        {
            var company = new List<List<Company>>();
            using (var client = new HttpClient())
            {
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                //client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                client.DefaultRequestHeaders.Connection.Add("keep-alive");

                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var response = client.GetAsync($"Employee/{SSFPatientUUID}/").Result;
                if (response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<EmployerRoot>(details);
                    company = data.company;
                }
            }
            return company;
        }

        //Krishna, 15thMarch'23 This is a new method to get the SSFEmployerList after patient Detail is loaded this method will get hit to fetch the list of employer,
        //code is same as the GetEmployerList() method (Not implementation of any interface method)
        private async Task<List<List<Company>>> GetSsfEmployerList(SSFDbContext ssfDbContext, string SSFPatientUUID)
        {
            var company = new List<List<Company>>();
            using (var client = new HttpClient())
            {
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                //client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                client.DefaultRequestHeaders.Connection.Add("keep-alive");

                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var response = client.GetAsync($"Employee/{SSFPatientUUID}/").Result;
                if (response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<EmployerRoot>(details);
                    company = data.company;
                }
            }
            return company;
        }

        public async Task<SSFClaimSubmissionOutput> SubmitClaim(SSFDbContext ssfDbContext, ClaimRoot claimRoot, SSFClaimResponseInfo responseInfo)
        {
            try
            {
                var client = new HttpClient();
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var jsonContent = JsonConvert.SerializeObject(claimRoot);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"Claim/", content);
                var result = "";
                var errorResult = new ErrorRoot();
                var errorMessage = "";
                SSFClaimResponse serializeData = new SSFClaimResponse();
                string responseStatus = ENUM_Danphe_HTTP_ResponseStatus.Failed;
                if (response.IsSuccessStatusCode)
                {
                    result = await response.Content.ReadAsStringAsync();
                    serializeData = JsonConvert.DeserializeObject<SSFClaimResponse>(result);
                    responseStatus = ENUM_Danphe_HTTP_ResponseStatus.OK;
                }
                else
                {
                    responseStatus = ENUM_Danphe_HTTP_ResponseStatus.Failed;
                    if (response.Content.Headers.ContentType?.MediaType != "text/html")
                    {
                        var errorString = await response.Content.ReadAsStringAsync();
                        errorResult = JsonConvert.DeserializeObject<ErrorRoot>(errorString);
                        if (errorResult != null && errorResult.issue.Count > 0)
                        {
                            errorResult.issue.ForEach(a =>
                            {
                                var tempErrorMsg = a.details.text;
                                errorMessage += tempErrorMsg + ",";
                            });
                        }
                        errorMessage = errorMessage.Substring(0, errorMessage.Length - 1);
                    }
                    else
                    {
                        var errorString = await response.Content.ReadAsStringAsync();
                        errorMessage = errorString;
                    }

                }

                List<SSFClaimResponseDetails> claimResponseList = new List<SSFClaimResponseDetails>();
                var PreviousClaimResponse = ssfDbContext.SSFClaimResponseDetail.Where(a => a.ClaimCode == responseInfo.ClaimCode).FirstOrDefault();

                string claimReferenceNo = null;
                if (serializeData.identifier != null)
                {
                    var indexOfClaimReferenceObject = serializeData.identifier.FindIndex(a => a.type.coding.Any(b => b.code.ToLower() == "mr")); //Krishna, 15thMay'23, This is to get the ClaimReferenceNo Object's index from identifier object 
                    claimReferenceNo = serializeData.identifier[indexOfClaimReferenceObject].value; //After getting indexOf ClaimReferenceNo's index we use that index to get ClaimReferenceNo
                }

                if (PreviousClaimResponse != null)
                {

                    //PreviousClaimResponse.ClaimReferenceNo = serializeData.id;
                    PreviousClaimResponse.ClaimReferenceNo = claimReferenceNo != null ? claimReferenceNo : serializeData.id;//Keeping it like this incase if claimReferenceNo is not found there will be uuid which can be used to find ClaimReferenceNo later.
                    PreviousClaimResponse.ClaimStatus = serializeData.status;
                    PreviousClaimResponse.ResponseData = result;
                    PreviousClaimResponse.ResponseStatus = response.IsSuccessStatusCode;
                    PreviousClaimResponse.ClaimedDate = DateTime.Now;
                    PreviousClaimResponse.ResponseDate = DateTime.Now;
                    PreviousClaimResponse.ClaimCount += 1;
                    await ssfDbContext.SaveChangesAsync();
                }
                else
                {
                    var item = new SSFClaimResponseDetails()
                    {
                        InvoiceNoCSV = responseInfo.InvoiceNoCSV,
                        PatientId = responseInfo.PatientId,
                        PatientCode = responseInfo.PatientCode,
                        ClaimCode = responseInfo.ClaimCode,
                        ClaimedDate = responseInfo.ClaimedDate,
                        ResponseData = result,
                        ClaimRequestDate = responseInfo.ClaimedDate,
                        ClaimStatus = serializeData.status,
                        ResponseDate = responseInfo.ClaimedDate,
                        ResponseStatus = response.IsSuccessStatusCode,
                        //ClaimReferenceNo = serializeData.id,
                        ClaimReferenceNo = claimReferenceNo != null ? claimReferenceNo : serializeData.id,//Keeping it like this incase if claimReferenceNo is not found there will be uuid which can be used to find ClaimReferenceNo later.
                        ClaimCount = 1
                    };
                    claimResponseList.Add(item);
                    ssfDbContext.SSFClaimResponseDetail.AddRange(claimResponseList);
                    await ssfDbContext.SaveChangesAsync();
                }

                SSFClaimSubmissionOutput outputResult = new SSFClaimSubmissionOutput
                {
                    ResponseStatus = responseStatus,
                    ErrorMessage = errorMessage
                };
                return outputResult;
            }
            catch (Exception ee)
            {
                throw ee;
            }
        }

        public async Task<EmployerRoot> GetClaimDetail(SSFDbContext ssfDbContext, string ClaimUUID)
        {
            using (var client = new HttpClient())
            {
                var SSFCred = GetSSFCredentials(ssfDbContext);
                var ISO_8859_1 = Encoding.GetEncoding("ISO-8859-1");
                var svcCredentials = Convert.ToBase64String(ISO_8859_1.GetBytes(SSFCred.SSFUsername + ":" + SSFCred.SSFPassword));
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add(SSFCred.SSFRemotekey, SSFCred.SSFRemoteValue);
                client.BaseAddress = new Uri(SSFCred.SSFurl);
                var response = client.GetAsync($"Claim/{ClaimUUID}/").Result;
                if (response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<EmployerRoot>(details);
                }
            }
            return new EmployerRoot();
        }

        public SSFCredentials GetSSFCredentials(SSFDbContext ssfDbContext)
        {
            SSFCredentials cred = new SSFCredentials();
            cred.SSFurl = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SSFConfiguration", "SSFurl");
            cred.SSFUsername = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SSFConfiguration", "SSFUsername");
            cred.SSFPassword = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SSFConfiguration", "SSFPassword");
            cred.SSFRemotekey = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SSFConfiguration", "SSFRemotekey");
            cred.SSFRemoteValue = GetCoreParameterValueByKeyName_String(ssfDbContext, "SSF", "SSFConfiguration", "SSFRemoteValue");
            return cred;
        }
        public static string GetCoreParameterValueByKeyName_String(SSFDbContext ssfDbContext, string paramGroup, string paramName, string keyNameOfJsonObj)
        {
            string retValue = null;

            var param = ssfDbContext.AdminParameters.Where(a => a.ParameterGroupName == paramGroup && a.ParameterName == paramName).FirstOrDefault();
            if (param != null)
            {
                string paramValueStr = param.ParameterValue;
                var data = DanpheJSONConvert.DeserializeObject<JObject>(paramValueStr);
                if (data != null)
                {
                    return data[keyNameOfJsonObj].Value<string>();
                }
            }

            return retValue;
        }
        public async Task<bool> IsClaimed(SSFDbContext ssfDbContext, long claimCode, int patientId)
        {
            var isClaimed = false;
            try
            {
                if(patientId == 0 || patientId == null)
                {
                    throw new Exception("Patient Detail is not provided to check Claim Status");
                }

                if(claimCode == null || claimCode == 0)
                {
                    isClaimed = false;
                }
                else
                {
                    var ssfClaimResponse = await ssfDbContext.SSFClaimResponseDetail.FirstOrDefaultAsync(ssfResDet => ssfResDet.PatientId == patientId && ssfResDet.ClaimCode == claimCode);
                    if (ssfClaimResponse != null && ssfClaimResponse.ResponseStatus == true)
                    {
                        isClaimed = true;
                    }
                }                
            }
            catch (Exception)
            {
                throw;
            }

            return isClaimed;
        }

        public async Task<PatientSchemeMapModel> GetSSFPatientDetailLocally(SSFDbContext sSFDbContext, int patientId, int schemeId)
        {
            try
            {
                var patientSchemeMap = new PatientSchemeMapModel();
                patientSchemeMap = await sSFDbContext.PatientSchemeMaps.FirstOrDefaultAsync(a => a.PatientId == patientId && a.SchemeId == schemeId); //PatientSchemeMaps Model has been removed.
                return patientSchemeMap;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
