window.onload = main;

var leaderboardGlobalVM;

function main()
{
	leaderboardGlobalVM = new LeaderboardGlobal();
	ko.applyBindings(leaderboardGlobalVM);
}
