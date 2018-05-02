                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class State {

	private string name;
	private List<string> handledSymbols;
	private List<Transition> transitions;

	public State(string name) {
		this.name = name;
		handledSymbols = new List<string> ();
		transitions = new List<Transition> ();
	}

	public string GetName() {
		return name;
	}

	public Transition GetTransitionOn(Symbol a) {
		if (handledSymbols.Contains(a.GetName())) {
			foreach (Transition t in transitions) {
				if (t.GetSymbolName() == a.GetName ()) {
					return t;
				}
			}
		}
		return null;
	}

	public bool TransitionIsDefined(String a) {
		return (handledSymbols.Contains (a));
	}

	public List<Transition> GetTransitions() {
		return transitions;
	}

	public List<string> GetHandledSymbols() {
		return handledSymbols;
	}

	public void AddTransition(Transition newTransition) {
		string symbolName = newTransition.GetSymbolName ();
		if (!handledSymbols.Contains (symbolName)) {
			handledSymbols.Add (symbolName);
			transitions.Add (newTransition);
		}
	}

	public void RemoveTransitionOn(string a) {
		if (handledSymbols.Contains (a)) {
			int index = handledSymbols.IndexOf (a);
			handledSymbols.RemoveAt (index);
			transitions.RemoveAt (index);
		}
	}
		
}
