﻿using System;
using System.Linq;
using EduNexBL.ENums;
using EduNexDB.Context;
using EduNexDB.Entites;

namespace EduNexBL.Services
{
    public class CouponService
    {
        private readonly EduNexContext _context;

        public CouponService(EduNexContext context)
        {
            _context = context;
        }

        // Method to generate a coupon
        public Coupon GenerateCoupon(decimal value, int numberOfUses)
        {
            var coupon = new Coupon
            {
                CouponCode = Guid.NewGuid().ToString().Substring(0, 8), // Generate a unique coupon code
                Value = value,
                NumberOfUses = numberOfUses
            };
            _context.Coupon.Add(coupon);
            _context.SaveChanges();
            return coupon;
        }

        // Method to consume a coupon
        public bool ConsumeCoupon(string couponCode, string ownerId, string ownerType)
        {
            var coupon = _context.Coupon.FirstOrDefault(c => c.CouponCode == couponCode && c.NumberOfUses > 0);
            if (coupon == null)
            {
                return false; // Coupon not found or all uses have been exhausted
            }

            // Get the wallet for the user or create one if it doesn't exist
            var wallet = _context.Wallets.FirstOrDefault(w => w.OwnerId == ownerId && w.OwnerType == ownerType);
            if (wallet == null)
            {
                wallet = new Wallet
                {

                    OwnerId = ownerId,
                    OwnerType = ownerType,
                    Balance = coupon.Value
                };
                _context.Wallets.Add(wallet);
                _context.SaveChanges();
            }
            else
            {
                wallet.Balance += coupon.Value;
            }

            // Create a transaction for coupon usage
            var transaction = new Transaction
            {
                TransactionType = IntegrationType.coupon.ToString(),
                Amount = coupon.Value,
                TransactionDate = DateTime.Now.ToString(),
                WalletId = wallet.WalletId // Set the wallet ID for the transaction
            };

            _context.Transactions.Add(transaction);

            coupon.NumberOfUses--;

            _context.SaveChanges();

            return true;
        }
    }
}