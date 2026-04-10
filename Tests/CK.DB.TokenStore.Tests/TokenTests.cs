using CK.Core;
using CK.DB.TokenStore.Tests.Helpers;
using CK.SqlServer;
using CK.Testing;
using Shouldly;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.DB.TokenStore.Tests;

[TestFixture]
public class TokenTests
{
    [Test]
    public void create_and_destroy()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>().ShouldNotBeNull();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var createResult = tokenStoreTable.Create( ctx, 1, info );
            createResult.Success.ShouldBeTrue();
            createResult.TokenId.ShouldBeGreaterThan( 0 );
            createResult.Token.ShouldNotBeEmpty();

            var checkResult = tokenStoreTable.Check( ctx, 1, createResult.Token );
            checkResult.IsValid().ShouldBeTrue();
            checkResult.TokenId.ShouldBe( createResult.TokenId );
            checkResult.LastCheckedDate.ShouldBe( DateTime.UtcNow, tolerance: TimeSpan.FromSeconds( 1 ) );
            checkResult.ValidCheckedCount.ShouldBe( 1 );

            tokenStoreTable.Destroy( ctx, 1, createResult.TokenId );
        }
    }

    [Test]
    public async Task create_and_destroy_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>().ShouldNotBeNull();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var createResult = await tokenStoreTable.CreateAsync( ctx, 1, info );
            createResult.Success.ShouldBeTrue();
            createResult.TokenId.ShouldBeGreaterThan( 0 );
            createResult.Token.ShouldNotBeEmpty();

            var checkResult = await tokenStoreTable.CheckAsync( ctx, 1, createResult.Token );
            checkResult.IsValid().ShouldBeTrue();
            checkResult.TokenId.ShouldBe( createResult.TokenId );
            checkResult.LastCheckedDate.ShouldBe( DateTime.UtcNow, tolerance: TimeSpan.FromSeconds( 1 ) );
            checkResult.ValidCheckedCount.ShouldBe( 1 );

            await tokenStoreTable.DestroyAsync( ctx, 1, createResult.TokenId );
        }
    }

    [Test]
    public void scoped_key_uniqueness()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>().ShouldNotBeNull();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var result1 = tokenStoreTable.Create( ctx, 1, info );
            result1.Success.ShouldBeTrue();
            var result2 = tokenStoreTable.Create( ctx, 1, info );
            result2.Success.ShouldBeFalse();
            info.TokenScope = "SomeOtherScope";
            var result3 = tokenStoreTable.Create( ctx, 1, info );
            result3.Success.ShouldBeTrue();
        }
    }

    [TestCase( "3712.ff92d601-b556-4f33-80b5-08d5a2915bb0" )]
    [TestCase( "wxcvbn.213e6220-3816-492c-830f-abeed5dd8c88" )]
    [TestCase( "3712.azertyuiop" )]
    [TestCase( "This is not.A valid token" )]
    [TestCase( "qsdfghjklm" )]
    [TestCase( null )]
    public void invalid_or_missing_token_does_not_check( string token )
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>().ShouldNotBeNull();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var result = tokenStoreTable.Check( ctx, 1, token );
            result.TokenId.ShouldBe( 0 );
            result.CreatedById.ShouldBe( 0 );
            result.TokenScope.ShouldBeNull();
            result.TokenKey.ShouldBeNull();
            result.Token.ShouldBe( token );
            result.ExpirationDateUtc.ShouldBe( Util.UtcMinValue );
            result.LastCheckedDate.ShouldBe( Util.UtcMinValue );
            result.ValidCheckedCount.ShouldBe( 0 );
        }
    }

    [Test]
    public void token_expiration_is_boosted_by_at_least_5_minutes_when_token_is_checked()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo( DateTime.UtcNow.AddMinutes( 2 ) );
            var result = tokenStoreTable.Create( ctx, 1, info );
            result.Success.ShouldBeTrue();

            var boosted = info.ExpirationDateUtc.AddMinutes( 5 );
            var checkResult = tokenStoreTable.Check( ctx, 1, result.Token );
            checkResult.TokenId.ShouldBeGreaterThan( 0 );
            boosted.ShouldBeLessThan( checkResult.ExpirationDateUtc );
        }
    }

    [Test]
    public void token_expiration()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo( DateTime.UtcNow.AddMilliseconds( 500 ) );
            var result = tokenStoreTable.Create( ctx, 1, info );
            result.Success.ShouldBeTrue();

            Thread.Sleep( 550 );
            var startInfo = tokenStoreTable.Check( ctx, 1, result.Token );
            startInfo.IsValid().ShouldBeFalse();
        }
    }

    [Test]
    public async Task token_expiration_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo( DateTime.UtcNow.AddMilliseconds( 500 ) );
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            result.Success.ShouldBeTrue();

            await Task.Delay( 550 );
            var startInfo = await tokenStoreTable.CheckAsync( ctx, 1, result.Token );
            startInfo.IsValid().ShouldBeFalse();
        }
    }

    [Test]
    public async Task token_expiration_and_ExtraData_set_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            var expiration = DateTime.UtcNow + TimeSpan.FromMinutes( 15 );
            result.Success.ShouldBeTrue();
            await tokenStoreTable.ActivateAsync( ctx, 1, result.TokenId, null, expiration );
            var startInfo = await tokenStoreTable.CheckAsync( ctx, 1, result.Token );
            startInfo.ExpirationDateUtc.ShouldBe( expiration, tolerance: TimeSpan.FromMilliseconds( 500 ) );

            await tokenStoreTable.SetExtraDataAsync( ctx, 1, result.TokenId, [0, 1, 2] );
            info = await tokenStoreTable.CheckAsync( ctx, 1, result.Token );

            info.Active.ShouldBe( startInfo.Active );
            info.CreatedById.ShouldBe( startInfo.CreatedById );
            info.ExpirationDateUtc.ShouldBe( startInfo.ExpirationDateUtc );
            info.Token.ShouldBe( startInfo.Token );
            info.TokenId.ShouldBe( startInfo.TokenId );
            info.TokenKey.ShouldBe( startInfo.TokenKey );
            info.TokenScope.ShouldBe( startInfo.TokenScope );
            info.ExtraData.ShouldBe( [0, 1, 2] );
            info.ValidCheckedCount.ShouldBe( startInfo.ValidCheckedCount + 1 );
            info.LastCheckedDate.ShouldBeGreaterThanOrEqualTo( startInfo.LastCheckedDate );

            await tokenStoreTable.SetExtraDataAsync( ctx, 1, result.TokenId, null );
            info = await tokenStoreTable.CheckAsync( ctx, 1, result.Token );

            info.Active.ShouldBe( startInfo.Active );
            info.CreatedById.ShouldBe( startInfo.CreatedById );
            info.ExpirationDateUtc.ShouldBe( startInfo.ExpirationDateUtc );
            info.Token.ShouldBe( startInfo.Token );
            info.TokenId.ShouldBe( startInfo.TokenId );
            info.TokenKey.ShouldBe( startInfo.TokenKey );
            info.TokenScope.ShouldBe( startInfo.TokenScope );
            info.ExtraData.ShouldBeNull();
            info.ValidCheckedCount.ShouldBe( startInfo.ValidCheckedCount + 2 );
            info.LastCheckedDate.ShouldBeGreaterThanOrEqualTo( startInfo.LastCheckedDate );
        }
    }

    [Test]
    public async Task invalid_set_expiration_date_raises_an_exception_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            var expiration = DateTime.UtcNow - TimeSpan.FromMinutes( 1 );
            result.Success.ShouldBeTrue();
            await Util.Awaitable( () => tokenStoreTable.ActivateAsync( ctx, 1, result.TokenId, null, expiration ) )
                       .ShouldThrowAsync<SqlDetailedException>();
        }
    }

    [Test]
    public async Task token_inactive_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var info = tokenStoreTable.GenerateTestInvitationInfo();
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            result.Success.ShouldBeTrue();
            await tokenStoreTable.ActivateAsync( ctx, 1, result.TokenId, false );
            var startInfo = await tokenStoreTable.CheckAsync( ctx, 1, result.Token );
            startInfo.IsValid().ShouldBeFalse();
        }
    }

    [Test]
    public async Task add_safe_time_superior_to_expiration_should_changeAsync()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var dateOriginExpirationToken = DateTime.UtcNow.AddSeconds( 10 );
            var info = tokenStoreTable.GenerateTestInvitationInfo( dateOriginExpirationToken );
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            result.Success.ShouldBeTrue();

            var startInfo = await tokenStoreTable.CheckAsync( ctx, 1, result.Token, 60 );
            startInfo.IsValid().ShouldBeTrue();

            startInfo.ExpirationDateUtc.ShouldNotBe( dateOriginExpirationToken );
        }
    }

    [Test]
    public async Task add_safe_time_inferior_to_expiration_should_not_change_Async()
    {
        var tokenStoreTable = SharedEngine.Map.StObjs.Obtain<TokenStoreTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var dateOriginExpirationToken = DateTime.UtcNow.AddSeconds( 60 );
            var info = tokenStoreTable.GenerateTestInvitationInfo( dateOriginExpirationToken );
            var result = await tokenStoreTable.CreateAsync( ctx, 1, info );
            result.Success.ShouldBeTrue();

            var startInfo = await tokenStoreTable.CheckAsync( ctx, 1, result.Token, 10 );
            startInfo.IsValid().ShouldBeTrue();

            startInfo.ExpirationDateUtc.ShouldBe( dateOriginExpirationToken, tolerance: TimeSpan.FromMilliseconds( 100 ) );
        }
    }
}
