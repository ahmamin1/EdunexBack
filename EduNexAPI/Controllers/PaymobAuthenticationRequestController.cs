﻿using AutoMapper;
using EduNexBL.Repository;
using EduNexBL.DTOs.PaymentDTOs;
using EduNexDB.Entites;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using EduNexBL.IRepository;
using EduNexBL.ENums;
using Amazon.S3.Model;
using Azure;
using System.Reflection;
using System.Data;
using Newtonsoft.Json.Linq;
using EduNexBL.UnitOfWork;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.VisualBasic;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EduNexAPI.Controllers
{
    public class PaymobAuthenticationRequestController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _client;
        internal OrderRequestDTO orderRequest;
        internal PaymentKeyCardRequestDTO paymentKeyRequest;
        internal string created_at = String.Empty;

        public PaymobAuthenticationRequestController(IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task UpdateWalletBalance(string userId, int amount)
        {
            try
            {
                // Get the user's wallet from the repository
                var userWallet = await _unitOfWork.WalletRepo.GetById(userId);

                if (userWallet != null)
                {
                    // Update the wallet balance
                    userWallet.Balance += amount;

                    // Update the wallet in the repository
                    await _unitOfWork.WalletRepo.UpdateWallet(userWallet);
                }
                else
                {
                    // Handle scenario where user's wallet is not found
                    Console.WriteLine($"User's wallet with ID {userId} not found.");
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                throw new Exception(ex.Message);
            }
        }

        private async Task CreateTransaction(string transactionType, string userId, int amount) 
        {
            try
            {
                Transaction transactionDTO = new Transaction();
                var wallet = await _unitOfWork.WalletRepo.GetById(userId); 
                if (wallet != null) 
                {
                    // Create a new transaction WalletId
                    transactionDTO = new Transaction()
                    {
                        WalletId = wallet.WalletId,
                        TransactionType = transactionType,
                        Amount = (decimal)amount,
                        TransactionDate = created_at
                    };

                    // Add the transaction to the repository
                    await _unitOfWork.TransactionRepo.Add(transactionDTO);

                    //Read the transaction
                    Console.WriteLine($"{transactionDTO.WalletId},{transactionDTO.TransactionType},{transactionDTO.TransactionDate},{transactionDTO.Amount}");
                }
                else
                {
                    Console.WriteLine($"Couldn't find the transaction related to user: {userId}");
                }               
            }
            catch (Exception ex)        
            {
                // Handle any exceptions
                throw new Exception(ex.Message);
            }
        }

        //Get Auth_token from Paymob
        public async Task<BaseResponseWithDataModel<string>> GetPaymobToken()
        {
            var AuthResponse = new BaseResponseWithDataModel<string>();
            AuthResponse.ErrorMsg = "";

            try
            {
                // Paymob API key
                string apiKey = _configuration["Paymob:ApiKey"];

                // Create JSON object with API key
                var requestData = new
                {
                    api_key = apiKey
                };

                // Convert object to JSON string
                string jsonRequest = JsonConvert.SerializeObject(requestData);

                // Create StringContent with JSON data
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // Set the base address for this request
                _client.BaseAddress = new Uri("https://accept.paymob.com/api/");

                // Send POST request to Paymob authentication endpoint
                HttpResponseMessage response = await _client.PostAsync("auth/tokens", content);

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response to extract token
                    dynamic responseObject = JsonConvert.DeserializeObject<AuthResponseModel>(responseContent);
                    string token = responseObject.token;
                    // Return the token
                    AuthResponse.Data = token;
                    return AuthResponse;
                }
                else
                {
                    // Handle error response from Paymob API
                    string errorMessage = $"Paymob API request failed with status code: {response.StatusCode}";
                    AuthResponse.ErrorMsg = errorMessage;
                    return AuthResponse;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                AuthResponse.ErrorMsg = ex.Message;
                return AuthResponse;
            }
        }
        
        //Create an order request and get order id from paymob
        public async Task<BaseResponseWithDataModel<string>> CreateOrder(int price)
        {            
            BaseResponseWithDataModel<string> idResponse = new BaseResponseWithDataModel<string>();
            try
            {
                // Get the Paymob token
                BaseResponseWithDataModel<string> tokenResponse = await GetPaymobToken();

                if (!string.IsNullOrEmpty(tokenResponse.ErrorMsg))
                {
                    string errorMessage = $"Paymob API request failed with status code: {tokenResponse.ErrorMsg}";
                    tokenResponse.ErrorMsg = errorMessage;
                    return tokenResponse;
                }

                string token = tokenResponse.Data;

                orderRequest = new(token,price);

                // Prepare the order request data
                var requestID = new
                {
                    auth_token = orderRequest.AuthToken,
                    delivery_needed = orderRequest.DeliveryNeeded,
                    amount_cents = orderRequest.AmountCents,
                    currency = orderRequest.Currency,
                    items = orderRequest.Items
                };

                // Convert object to JSON string
                string jsonRequest = JsonConvert.SerializeObject(requestID);

                // Create StringContent with JSON data
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // Create a new HttpClient instance
                using var client = new HttpClient();
                client.BaseAddress = new Uri("https://accept.paymob.com/api/ecommerce/");

                // Send POST request to Paymob order creation endpoint
                HttpResponseMessage response = await client.PostAsync("orders", content);

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response to extract token
                    dynamic responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    int id = responseObject.id;
                    created_at = responseObject.created_at;
                    //payment_method = responseObject.payment_method;
                    idResponse.Data = id.ToString();
                    // Return success status
                    return idResponse;
                }
                else
                {
                    // Handle error response from Paymob API
                    string errorMessage = $"Paymob order creation failed with status code: {response.StatusCode}";
                    idResponse.ErrorMsg = errorMessage;
                    return idResponse;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                idResponse.ErrorMsg = ex.Message;
                return idResponse;
            }
        }

        //GetPaymentKey based on Integration id
        public async Task<BaseResponseWithDataModel<string>> GetPaymentKeyBasedOnIntegrationId(IntegrationType integrationType, int price, string userId)
        {
            BaseResponseWithDataModel<string> paymentKeyResponse = new BaseResponseWithDataModel<string>();
            var amountCents = price;
            var userID = userId;
            string id = String.Empty;
            int integration_ID = 0;

            int GetIntegrationId(IntegrationType integrationType)
            {
                switch (integrationType)
                {
                    case IntegrationType.Online_Card:
                        return int.Parse(_configuration["Paymob:OnlineCardIntegrationId"]);
                    case IntegrationType.Mobile_Wallet:
                        return int.Parse(_configuration["Paymob:MobileWalletIntegrationId"]);
                    case IntegrationType.Accept_Kiosk:
                        return int.Parse(_configuration["Paymob:AcceptKioskIntegrationId"]);
                    default:
                        return 0;
                }
            }                     

            try
            {
                // Get the Paymob token
                BaseResponseWithDataModel<string> idResponse = await CreateOrder(amountCents);

                if (!string.IsNullOrEmpty(idResponse.ErrorMsg))
                {
                    string errorMessage = $"Paymob API request failed with status code: {idResponse.ErrorMsg}";
                    idResponse.ErrorMsg = errorMessage;
                    return idResponse;
                }

                id = idResponse.Data;
                integration_ID = GetIntegrationId(integrationType);

                paymentKeyRequest = new(orderRequest.AuthToken,orderRequest.AmountCents.ToString(), int.Parse(id), integration_ID);

                // Prepare the order request data
                var requestKey = new
                {
                    auth_token = paymentKeyRequest.auth_token,
                    amount_cents = paymentKeyRequest.amount_cents,
                    expiration = paymentKeyRequest.expiration,
                    order_id = paymentKeyRequest.order_id,
                    billing_data = paymentKeyRequest.billing_data,
                    currency = paymentKeyRequest.currency,
                    integration_id = paymentKeyRequest.integration_id
                };

                // Convert object to JSON string
                string jsonRequest = JsonConvert.SerializeObject(requestKey);

                // Create StringContent with JSON data
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // Create a new HttpClient instance
                using var client = new HttpClient();
                client.BaseAddress = new Uri("https://accept.paymob.com/api/acceptance/");

                // Send POST request to Paymob order creation endpoint
                HttpResponseMessage response = await client.PostAsync("payment_keys", content);

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response to extract token
                    dynamic responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    string paymentKey = responseObject.token;
                    paymentKeyResponse.Data = paymentKey;

                    // Return success status
                    return paymentKeyResponse;
                }
                else
                {
                    // Handle error response from Paymob API
                    string errorMessage = $"Paymob order creation failed with status code: {response.StatusCode}";
                    paymentKeyResponse.ErrorMsg = errorMessage;
                    return paymentKeyResponse;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                paymentKeyResponse.ErrorMsg = ex.Message;
                return paymentKeyResponse;
            }
        }

        [HttpGet("GetIFrameForOnlineCardPayment")]
        public async Task<BaseResponseWithDataModel<string>> GetIFrameForOnlineCardPayment(int price, string userId)
        {
            BaseResponseWithDataModel<string> IFrameAndPaymentKey = new BaseResponseWithDataModel<string>();
            IntegrationType integrationType = IntegrationType.Online_Card;
            string IFrame = "https://accept.paymob.com/api/acceptance/iframes/838672?payment_token=";
            try
            {
                var PaymentKey = await GetPaymentKeyBasedOnIntegrationId(integrationType, price, userId);
                if (string.IsNullOrEmpty(PaymentKey.ErrorMsg))
                {
                    var _paymentKey = PaymentKey.Data;
                    string iframepaymentkey = IFrame + _paymentKey;
                    IFrameAndPaymentKey.Data = iframepaymentkey;

                    await UpdateWalletBalance(userId, price);
                    await CreateTransaction(integrationType.ToString(), userId, price);

                    return IFrameAndPaymentKey;
                }
                else
                {
                    IFrameAndPaymentKey.ErrorMsg = PaymentKey.ErrorMsg;
                    return IFrameAndPaymentKey;
                }
            }
            catch (Exception ex)
            {
                IFrameAndPaymentKey.ErrorMsg += ex.Message;
                return IFrameAndPaymentKey;
            }
        }
        
        [HttpPost("GetURLForMobileWalletPayment")]
        public async Task<BaseResponseWithDataModel<string>> RequestMobileWalletURL(int price, string userId, string walletMobileNumber)
        {
            BaseResponseWithDataModel<string> redirectURL = new BaseResponseWithDataModel<string>();
            IntegrationType integrationType = IntegrationType.Mobile_Wallet;           
            try
            {
                var paymentToken = await GetPaymentKeyBasedOnIntegrationId(integrationType, price, userId);

                if (!string.IsNullOrEmpty(paymentToken.ErrorMsg))
                {
                    string errorMessage = $"Paymob API request failed with status code: {paymentToken.ErrorMsg}";
                    paymentToken.ErrorMsg = errorMessage;
                    return paymentToken;
                }

                string token = paymentToken.Data;

                var walletPayRequest = new
                {
                    source = new
                    {
                        identifier = walletMobileNumber,
                        subtype = "WALLET"
                    },
                    payment_token = token
                };

                // Convert object to JSON string
                string jsonRequest = JsonConvert.SerializeObject(walletPayRequest);

                // Create StringContent with JSON data
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // Create a new HttpClient instance
                using var client = new HttpClient();
                client.BaseAddress = new Uri("https://accept.paymob.com/api/acceptance/payments/pay");

                // Send POST request to Paymob order creation endpoint
                HttpResponseMessage response = await client.PostAsync("pay", content);

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response to extract token
                    dynamic responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    string walletredirectURL = responseObject.redirect_url;
                    redirectURL.Data = walletredirectURL;

                    await UpdateWalletBalance(userId, price);
                    await CreateTransaction(integrationType.ToString(), userId, price);

                    // Return success status
                    return redirectURL;
                }
                else
                {
                    // Handle error response from Paymob API
                    string errorMessage = $"Paymob order creation failed with status code: {response.StatusCode}";
                    redirectURL.ErrorMsg = errorMessage;
                    return redirectURL;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                redirectURL.ErrorMsg = ex.Message;
                return redirectURL;
            }
        }

        [HttpPost("GetReferenceNumberForKioskPayment")]
        public async Task<BaseResponseWithDataModel<string>> RequestKioskPaymentReferenceNumber(int price, string userId)
        {
            BaseResponseWithDataModel<string> referenceNumber = new BaseResponseWithDataModel<string>();
            IntegrationType integrationType = IntegrationType.Accept_Kiosk;
            try
            {
                var paymentToken = await GetPaymentKeyBasedOnIntegrationId(integrationType, price, userId);

                if (!string.IsNullOrEmpty(paymentToken.ErrorMsg))
                {
                    string errorMessage = $"Paymob API request failed with status code: {paymentToken.ErrorMsg}";
                    paymentToken.ErrorMsg = errorMessage;
                    return paymentToken;
                }

                string token = paymentToken.Data;

                var kioskPayRequest = new
                {
                    source = new
                    {
                        identifier = "AGGREGATOR",
                        subtype = "AGGREGATOR"
                    },
                    payment_token = token
                };

                // Convert object to JSON string
                string jsonRequest = JsonConvert.SerializeObject(kioskPayRequest);

                // Create StringContent with JSON data
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // Create a new HttpClient instance
                using var client = new HttpClient();
                client.BaseAddress = new Uri("https://accept.paymob.com/api/acceptance/payments/pay");

                // Send POST request to Paymob order creation endpoint
                HttpResponseMessage response = await client.PostAsync("pay", content);

                // Check if request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response to extract token
                    dynamic responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    string kioskReferenceNumber = responseObject.data.bill_reference;
                    referenceNumber.Data = kioskReferenceNumber;

                    await UpdateWalletBalance(userId, price);
                    await CreateTransaction(integrationType.ToString(), userId, price);

                    // Return success status
                    return referenceNumber;
                }
                else
                {
                    // Handle error response from Paymob API
                    string errorMessage = $"Paymob order creation failed with status code: {response.StatusCode}";
                    referenceNumber.ErrorMsg = errorMessage;
                    return referenceNumber;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                referenceNumber.ErrorMsg = ex.Message;
                return referenceNumber;
            }
        }

        [HttpGet("GetWalletBalance")]
        public async Task<IActionResult> GetWalletBalance(string ownerId)
        {
            try
            {
                Wallet displayedWallet = await _unitOfWork.WalletRepo.GetById(ownerId);

                if (displayedWallet != null)
                {
                    return Ok(displayedWallet.Balance);
                }
                else
                {
                    return NotFound($"Wallet not found for the provided owner with data: id = {ownerId}");
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        [HttpGet("GetAllWalletByOwnerType")]
        public async Task<IActionResult> GetALLWalletsByOwnerType(OwnerType ownerType)
        {
            try
            {
                var walletsOfOwnerType = await _unitOfWork.WalletRepo.GetALLWalletsByOwnerType(ownerType);
                if (!walletsOfOwnerType.IsNullOrEmpty())
                {
                    return Ok(walletsOfOwnerType);
                }
                else
                {
                    return NotFound("Wallets not found");
                }
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("GetByOwnerIdAndOwnerType")]
        public async Task<IActionResult>  GetByOwnerIdAndOwnerType(string id, OwnerType ownerType)
        {
            try
            {
                var ownerWallet = await _unitOfWork.WalletRepo.GetByOwnerIdAndOwnerType(id, ownerType);
                if (ownerWallet != null)
                {
                    return Ok(ownerWallet);
                }
                else
                {
                    return NotFound("Wallet not found");
                }
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("GetALLWallets")]
        public async Task<IActionResult> GetALLWallets()
        {
            try
            {
                var Wallets = await _unitOfWork.WalletRepo.GetALLWallets();
                if (!Wallets.IsNullOrEmpty())
                {
                    return Ok(Wallets);
                }
                else
                {
                    return NotFound("Wallets not found");
                }
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("GetStudentTransactions")]
        public async Task<IActionResult> GetStudentTransactions(string StudId)
        {
            try
            {
                var Transactions = await _unitOfWork.TransactionRepo.GetTransactionsByStudentId(StudId);
                if (!Transactions.IsNullOrEmpty())
                {
                    return Ok(Transactions);
                }
                else
                {
                    return NotFound("Transactions not found");
                }
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("GetTransactionById")]
        public async Task<IActionResult> GetTransactionById(int id)
        {
            try
            {
                var transaction = await _unitOfWork.TransactionRepo.GetById(id);
                if (transaction != null)
                {
                    return Ok(transaction);
                }
                else
                {
                    return NotFound("Transactions not found");
                }
            }
            catch(Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("GetALLTransactions")]
        public async Task<IActionResult> GetALLTransactions()
        {
            try
            {
                var transactions = await _unitOfWork.TransactionRepo.GetAllTransactions();
                if (!transactions.IsNullOrEmpty())
                {
                    return Ok(transactions);
                }
                else
                {
                    return NotFound("Transactions not found");
                }
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("GeneratNewWallet")]
        public async Task<IActionResult> GeneratNewWallet(string id)
        {
            try
            {
                var newWallet = _unitOfWork.WalletRepo.GenerateWallet(id);
                if (newWallet != null)
                {
                    return Ok(newWallet);
                }
                return NotFound("User Not Found");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("PurchaseCourse")]
        public async Task<IActionResult> PurchaseCourse(string studentId, int courseId,[FromQuery] string[]? couponCodes)
        {
            try
            {
                var enrollmentResult = await _unitOfWork.CourseRepo.EnrollStudentInCourse(studentId, courseId, couponCodes);

                switch (enrollmentResult)
                {
                    case EnrollmentResult.Success:
                        return Ok("Course purchased successfully.");
                    case EnrollmentResult.AlreadyEnrolled:
                        return Conflict("Student is already enrolled in the course.");
                    case EnrollmentResult.InsufficientBalance:
                        return BadRequest("Insufficient balance to purchase the course.");
                    case EnrollmentResult.InvalidCoupon:
                        return BadRequest("Attempting to use an invalid coupon.");
                    case EnrollmentResult.StudentNotFound:
                        return NotFound("Student not found.");
                    case EnrollmentResult.CourseNotFound:
                        return NotFound("Course not found.");
                    default:
                        return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred : {ex.Message}");
            }
        }
    }
}
