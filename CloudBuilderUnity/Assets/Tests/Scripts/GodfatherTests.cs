using System;
using UnityEngine;
using CotcSdk;
using System.Reflection;
using IntegrationTests;

public class GodfatherTests : TestBase {

	[InstanceMethod(typeof(GodfatherTests))]
	public string TestMethodName;

	void Start() {
		RunTestMethod(TestMethodName);
	}

	[Test("Creates two gamers, with one who generates a godfather code and associates it to the other gamer. Then tests all godfather functionality.")]
	public void ShouldAssociateGodfather(Cloud cloud) {
		Login2NewUsers(cloud, (gamer1, gamer2) => {
			// P1 generates a code and associates P2 with it
			gamer1.Godfather.GenerateCode()
			// Use code
			.ExpectSuccess(genCode => gamer2.Godfather.UseCode(genCode))
			.ExpectSuccess(dummy => gamer2.Godfather.GetGodfather())
			.ExpectSuccess(result => {
				Assert(result.GamerId == gamer1.GamerId, "P1 should be godfather");
				return gamer1.Godfather.GetGodchildren();
			})
			.ExpectSuccess(result => {
				Assert(result.Count == 1, "Should have only one godchildren");
				Assert(result[0].GamerId == gamer2.GamerId, "P2 should be godchildren");
				CompleteTest();
			});
		});
	}

	[Test("Creates an user who tries to redeem a code generated by himself.")]
	public void ShouldNotAssociateGodfatherWithHimself(Cloud cloud) {
		LoginNewUser(cloud, gamer => {
			// Should fail to godfather himself
			gamer.Godfather.GenerateCode()
			.ExpectSuccess(genCode => {
				gamer.Godfather.UseCode(genCode)
				.ExpectFailure(usedCode => {
					Assert(usedCode.HttpStatusCode == 400, "Expected HTTP 400");
					Assert(usedCode.ServerData["name"] == "cantBeSelfGodchild", "Expected cantBeSelfGodchild");
					CompleteTest();
				});
			});
		});
	}

	#region Private
	private string RandomBoardName() {
		return "board-" + Guid.NewGuid().ToString();
	}
	#endregion
}
