var LeaderboardAPI = {
	get: function(xToken, id, skip, take) {
		var urlEnd = "";
		if (id)
		{
			urlEnd += "/"+id+"/"+skip+"/"+take;
		}
		return $.ajax("https://api.stormancer.com/"+accountId+"/"+applicationName+"/_admin/leaderboard"+urlEnd, {
			type: 'GET',
			contentType: "application/json",
			dataType: "json",
			headers: {
				"x-token": xToken,
				"x-version": "1.0"
			}
		});
	}
};
