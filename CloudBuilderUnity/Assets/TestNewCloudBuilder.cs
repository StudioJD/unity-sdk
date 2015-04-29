﻿using UnityEngine;
using System.Collections;
using CloudBuilderLibrary;

public class TestNewCloudBuilder : MonoBehaviour {

	// Use this for initialization
	void Start() {
		Bundle config = Bundle.CreateObject();
		config["key"] = "cloudbuilder-key";
		config["secret"] = "azerty";
//		config["env"] = "http://195.154.227.44:8000";
//		config["env"] = "http://localhost:8000";
		config["env"] = "https://sandbox-api[id].clanofthecloud.mobi";
		config["httpVerbose"] = true;
//		config["httpTimeout"] = 2000;
		CloudBuilder.Clan.Setup(config);
	}
	
	// Update is called once per frame
	void Update() {
	
	}

	public void DoLogin() {
		CloudBuilder.Clan.LoginAnonymous();
    }
}