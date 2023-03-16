
using Grpc.Core;

namespace Billing.Services
{
    public class BillingService : Billing.BillingBase
    {
        private static List<UserProfile> users = new() { new UserProfile { Name = "boris", Rating = 5000 },
            new UserProfile { Name = "maria", Rating = 1000 }, new UserProfile { Name = "oleg", Rating = 800 } };
        private static List<Coin> coins = new();

        public override async Task ListUsers(None request, IServerStreamWriter<UserProfile> responseStream, ServerCallContext context)
        {
            foreach (var user in users)
            {
                await responseStream.WriteAsync(user);
            }
        }

        public override async Task<Response> CoinsEmission(EmissionAmount request, ServerCallContext context)
        {
            var allRating = users.Sum(u => u.Rating);
            var curAmount = request.Amount;
            var fractionalParts = 0d;

            foreach (var user in users)
            {
                var rawMoveAmount = request.Amount * (double)user.Rating / allRating;
                var doubleMoveAmount = Math.Round(fractionalParts + rawMoveAmount);
                if (doubleMoveAmount > Math.Round(rawMoveAmount))
                    fractionalParts = 0;
                else
                    fractionalParts += rawMoveAmount - (int)rawMoveAmount;

                var moveAmount = (int)doubleMoveAmount;
                if (moveAmount == 0) moveAmount = 1;
                user.Amount += moveAmount;

                for (var i = 0; i < moveAmount; i++)
                    coins.Add(new Coin { Id = coins.Count, History = user.Name });

                curAmount -= moveAmount;
            }

            return new Response { Status = Response.Types.Status.Ok, Comment = "Coins were emissed" };
        }

        public override async Task<Response> MoveCoins(MoveCoinsTransaction request, ServerCallContext context)
        {
            var srcUser = users.Find(u => u.Name == request.SrcUser);
            var dstUser = users.Find(u => u.Name == request.DstUser);

            if (srcUser is null || dstUser is null)
                return new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = string.Format("User {0} not found", srcUser is null ? request.SrcUser : request.DstUser)
                };

            if (srcUser.Amount < request.Amount)
                return new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = string.Format("User {0} doesn't have enough amount", request.SrcUser)
                };

            srcUser.Amount -= request.Amount;
            dstUser.Amount += request.Amount;

            var movedCoins = coins.FindAll(c => c.History.EndsWith(srcUser.Name));
            foreach (var coin in movedCoins)
                coin.History += string.Format(";{0}", dstUser.Name);

            return new Response { Status = Response.Types.Status.Ok, Comment = "Coins were moved" };
        }

        public override async Task<Coin> LongestHistoryCoin(None request, ServerCallContext context)
        {
            var maxCoin = coins.MaxBy(c => c.History.Split(';').Length);
            if (maxCoin is null)
                return new Coin { Id = -1, History = string.Empty };

            return maxCoin;
        }
    }
}
