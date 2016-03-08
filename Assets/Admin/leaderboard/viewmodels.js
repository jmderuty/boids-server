function LeaderboardGlobal() {
	this.leaderboards = ko.observableArray();
	this.selectedLeaderboard = ko.observable();

	LeaderboardAPI.get(xToken).then(function(leaderboards) {
		for (var i=0; i<3; i++)
		{
			var leaderboard = leaderboards[i];
			leaderboardGlobalVM.leaderboards.push(new LeaderboardViewModel(leaderboard.id, leaderboard.name));
		}
	}).fail(function(e) {
		console.log("leaderboard API get error:", e);
	});
}

function LeaderboardViewModel(id, name, description, scores)
{
	this.id = ko.observable(id || null);
	this.name = ko.observable(name || "");
	this.description = ko.observable(description || "");
	this.scores = ko.observableArray(scores || []);
}

LeaderboardViewModel.prototype.select = function()
{
	leaderboardGlobalVM.selectedLeaderboard(this);
	LeaderboardAPI.get(xToken, this.id, 0, 1000).then(function(leaderboard){
		this.scores(leaderboard.scores);
	}).fail(function(e){
		console.log("leaderboard API get error", e);
	});
};

function ScoreViewModel(userid, username, score, leaderboard)
{
	this.userid = ko.observable(id || null);
	this.username = ko.observable(username || "");
	this.score = ko.observable(score || null);
	this.leaderboard = ko.observable(leaderboard || null);
}
