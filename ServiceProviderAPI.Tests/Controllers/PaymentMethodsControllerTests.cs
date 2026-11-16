using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Tests.Controllers;

// Guards the consumer/payout split: customers may save only a UPI handle, never bank details.
public class PaymentMethodsControllerTests
{
    private static PaymentMethodRequest Upi(string vpa = "asha@okbank") =>
        new() { Type = "UPI", UpiVpa = vpa };

    private static PaymentMethodRequest Bank() =>
        new() { Type = "Bank", BankAccountHolderName = "Asha", BankAccountNumber = "1234567890", BankIfsc = "SBIN0001234" };

    [Fact]
    public async Task Create_Consumer_Bank_IsRejected()
    {
        var controller = new PaymentMethodsController(TestSupport.NewDb()).AsUser(1, "User");

        var result = await controller.Create(Bank());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_Consumer_Upi_Succeeds()
    {
        var db = TestSupport.NewDb();
        var controller = new PaymentMethodsController(db).AsUser(1, "User");

        var result = await controller.Create(Upi());

        Assert.IsType<CreatedAtActionResult>(result);
        var pm = await db.PaymentMethods.SingleAsync();
        Assert.Equal(1, pm.UserId);
        Assert.Equal("asha@okbank", pm.UpiVpa);
    }

    [Fact]
    public async Task Create_Consumer_Upi_DropsAnyBankFields()
    {
        var db = TestSupport.NewDb();
        var controller = new PaymentMethodsController(db).AsUser(1, "User");

        // A consumer sends UPI but also smuggles bank fields — they must not be persisted.
        var req = new PaymentMethodRequest
        {
            Type = "UPI", UpiVpa = "asha@okbank",
            BankAccountNumber = "999999", BankIfsc = "HDFC0000001", BankAccountHolderName = "Asha",
        };
        await controller.Create(req);

        var pm = await db.PaymentMethods.SingleAsync();
        Assert.Null(pm.BankAccountNumber);
        Assert.Null(pm.BankIfsc);
        Assert.Null(pm.BankAccountHolderName);
    }

    [Fact]
    public async Task Create_Pro_Bank_Succeeds()
    {
        var db = TestSupport.NewDb();
        var controller = new PaymentMethodsController(db).AsUser(2, "Pro");

        var result = await controller.Create(Bank());

        Assert.IsType<CreatedAtActionResult>(result);
        var pm = await db.PaymentMethods.SingleAsync();
        Assert.Equal(2, pm.ProId);
        Assert.Equal("1234567890", pm.BankAccountNumber);
    }
}
