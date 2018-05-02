﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Model {
	
	public static float[] parameters = new float[] {0.05f,0.05f,0.2f,0.1f,1.0f,0.9f,0.5f,0.5f}; //alpha,beta,gamma,zeta,eta,kappa,nu,tau

	private List<string> Sigma;
	private List<string> Delta;
	private List<State> states;
	private List<State> reward;
	private List<State> punishment;
	private State curState, lastState, anchorState;
	private Symbol curOutput, lastOutput, curSymbol, lastSymbol;
	private List<Symbol> curInput;
	private List<Symbol> lastInput;
	private float timeSinceLastInput = 0f;
	private float lastTime = 0f;
	private Stack<Transition> markedDistributions;
	private List<string> markedSymbols;
	private List<Transition> conditioned;
	private List<string> inputSymbols;
	private List<string> lastInputSymbols;

	public Model(List<string> Sigma, List<string> Delta, List<State> states, State startingState) {
		this.Sigma = Sigma;
		this.Delta = Delta;
		this.states = states;
		curState = startingState;
		anchorState = curState;
		lastState = startingState;
		lastOutput = Symbol.Epsilon;
		curOutput = Symbol.Epsilon;
		curSymbol = Symbol.Epsilon;
		lastSymbol = Symbol.Epsilon;
		reward = new List<State> ();
		punishment = new List<State> ();
		curInput = new List<Symbol> ();
		lastInput = new List<Symbol> ();
		markedDistributions = new Stack<Transition> ();
		markedSymbols = new List<string>();
		conditioned = new List<Transition> ();
		inputSymbols = new List<string> ();
		lastInputSymbols = new List<string> ();

		lastTime = Time.time;
		timeSinceLastInput = Time.time - lastTime;
	}

	private bool InSigma(Symbol a) {
		return Sigma.Contains (a.GetName ());
	}

	private bool InDelta(Symbol o) {
		return Delta.Contains (o.GetName ());
	}

	public Symbol GetOutput() {
		return null;
	}

	public Symbol TakeInput(List<Symbol> inputs) {
		timeSinceLastInput = Time.time - lastTime;
		lastTime = Time.time;
		lastInput = curInput;
		curInput = inputs;
		if (timeSinceLastInput >= parameters [7]) {
			if (curState.TransitionIsDefined (Symbol.Epsilon.GetName ())) {
				Transition t = curState.GetTransitionOn (Symbol.Epsilon);
				if (t.IsTemporary ()) {
					t.SetTemporary (false);
				}
				lastState = curState;
				curState = curState.GetTransitionOn (Symbol.Epsilon).GetEndState();
			}
			anchorState = curState;
			lastSymbol = Symbol.Epsilon;
			lastOutput = Symbol.Epsilon;
			curOutput = Symbol.Epsilon;
			markedDistributions = new Stack<Transition>();
			markedSymbols = new List<string>();
			return (TakeInput (inputs));
		} else {
			curSymbol = GetStrongesSymbol ();
			CreateTransitions ();
			lastOutput = curOutput;
			Transition curTransition = curState.GetTransitionOn (curSymbol);
			curOutput = curTransition.GetOutputDistribution ().GetOutputSymbol ((curSymbol.GetValue()*curTransition.GetConfidence())/(1+curTransition.GetConfidence()));
			markedDistributions.Push (curTransition);
			markedSymbols.Add (lastOutput.GetName());
			UpdateExpectations ();
			lastState = curState;
			curState = curState.GetTransitionOn (curSymbol).GetEndState ();
			lastSymbol = curSymbol;
			if (reward.Contains (curState)) {
				ApplyReward ();
			} else if (punishment.Contains (curState)) {
				ApplyPunishment ();
			} else {
				ApplyConditioning ();
			}
		}
		return curOutput;
	}

	private Symbol GetStrongesSymbol() {
		inputSymbols = new List<string> ();
		lastInputSymbols = new List<string> ();
		Symbol strongest = Symbol.Epsilon;
		foreach (Symbol symbol in curInput) {
			if (symbol.GetValue () > strongest.GetValue ()) {
				strongest = symbol;
			}
			inputSymbols.Add (symbol.GetName ());
		}
		foreach (Symbol symbol in lastInput) {
			lastInputSymbols.Add (symbol.GetName ());
		}
		return strongest;
	}

	public void CreateTransitions() {
		if (curState.TransitionIsDefined (Symbol.Epsilon.GetName ())) {
			curState.RemoveTransitionOn (Symbol.Epsilon.GetName ());
		}
		foreach (Symbol symbol in curInput) {
			if (!curState.TransitionIsDefined (symbol.GetName())) {
				State newState = new State ("" + states.Count);
				Transition newTransition = new Transition (curState, newState, symbol.GetName(), Delta);
				Transition tempTransition = new Transition (newState, anchorState, Symbol.Epsilon.GetName (), Delta);
				newState.AddTransition (newTransition);
				newState.AddTransition (tempTransition);
				foreach (State state in states) {
					if (state.TransitionIsDefined (symbol.GetName ())) {
						newTransition.SetDistribution (state.GetTransitionOn (symbol).GetOutputDistribution ());
						newTransition.getExpectations ().SetConfidence (state.GetTransitionOn (symbol).getExpectations ().GetConfidence ());
						if (reward.Contains (state)) {
							reward.Add (newState);
						} else if (punishment.Contains (state)) {
							punishment.Add (newState);
						}
						break;
					}
				}
				states.Add (newState);
			}
		}
	}

	public void UpdateExpectations() {
		Transition currentTransition = curState.GetTransitionOn (curSymbol);
		Transition lastTransition = lastState.GetTransitionOn (lastSymbol);

		if (lastTransition.getExpectations ().DoesExpectancyExistWith (currentTransition)) {
			lastTransition.getExpectations ().StrengthenExpectencyWith (currentTransition);
			currentTransition.getExpectations ().StrengthenExpectencyWith (lastTransition);
		} else {
			lastTransition.getExpectations ().CreateExpectancyWith (currentTransition);
			currentTransition.getExpectations ().CreateExpectancyWith (lastTransition);
		}

		foreach (string name in Sigma) {
			Symbol symbol = new Symbol (name, 0.0f);
			Transition symbolTransition = curState.GetTransitionOn (symbol);
			if (lastTransition.getExpectations ().DoesExpectancyExistWith (symbolTransition) && !inputSymbols.Contains(name)) {
				lastTransition.getExpectations ().WeakenExpectencyWith (symbolTransition);
				symbolTransition.getExpectations ().WeakenExpectencyWith (lastTransition);
			}
			foreach (State state in states) {
				if (state != lastState && name != lastSymbol.GetName ()) {
					Transition anyTransition = state.GetTransitionOn (symbol);
					if (lastTransition.getExpectations ().DoesExpectancyExistWith (anyTransition)) {
						lastTransition.getExpectations ().WeakenExpectencyWith (anyTransition);
						anyTransition.getExpectations ().WeakenExpectencyWith (lastTransition);
					}
				}
			}
			foreach (string nameB in Sigma) {
				if (name != nameB) {
					Symbol symbolB = new Symbol (nameB, 0.0f);
					Transition aTransition = curState.GetTransitionOn (symbol);
					Transition bTransition = curState.GetTransitionOn (symbolB);
					if (inputSymbols.Contains (name) && inputSymbols.Contains (nameB)) {
						if (aTransition.getExpectations ().DoesExpectancyExistWith (bTransition)) {
							aTransition.getExpectations ().StrengthenExpectencyWith (aTransition);
							bTransition.getExpectations ().StrengthenExpectencyWith (bTransition);
						} else {
							aTransition.getExpectations ().CreateExpectancyWith (bTransition);
							bTransition.getExpectations ().CreateExpectancyWith (aTransition);
						}
					} else if (inputSymbols.Contains (name) || inputSymbols.Contains (nameB)) {if (aTransition.getExpectations ().DoesExpectancyExistWith (bTransition)) {
							aTransition.getExpectations ().WeakenExpectencyWith (aTransition);
							bTransition.getExpectations ().WeakenExpectencyWith (bTransition);
						}
					}
				}
			}
		}
	}

	public void ApplyConditioning() {
		if (lastOutput != Symbol.Epsilon && curOutput != lastOutput) {
			foreach (string name in Sigma) {
				Symbol symbol = new Symbol (name, 0.0f);
				Transition lastTransition = lastState.GetTransitionOn (lastSymbol);
				Transition symbolTransition = lastState.GetTransitionOn (symbol);
				if (lastInputSymbols.Contains (name)) {
					if (lastTransition.getExpectations ().DoesExpectancyExistWith (symbolTransition)) {
						float change = parameters [2] * curSymbol.GetValue () / symbolTransition.GetConfidence ();
						symbolTransition.GetOutputDistribution ().UpdateSymbolProbability (lastOutput.GetName(), change);
						if (conditioned.Contains (symbolTransition)) {
							conditioned.Add (symbolTransition);
							symbolTransition.getExpectations ().AddToConfidence (parameters [2] * curSymbol.GetValue ());
							UpdateConditioning (lastState,symbol,curSymbol.GetValue()/symbolTransition.GetConfidence());
						}
					}
				}
				foreach (State state in states) {
					Transition seekingTransition = state.GetTransitionOn (symbol);
					if (seekingTransition.GetEndState () == lastState) {
						if (lastTransition.getExpectations ().DoesExpectancyExistWith (seekingTransition)) {
							float change = parameters [2] * curSymbol.GetValue () / seekingTransition.GetConfidence ();
							seekingTransition.GetOutputDistribution ().UpdateSymbolProbability (lastOutput.GetName(), change);
							if (conditioned.Contains (seekingTransition)) {
								conditioned.Add (seekingTransition);
								seekingTransition.getExpectations ().AddToConfidence (parameters [2] * curSymbol.GetValue ());
								UpdateConditioning (state,symbol,curSymbol.GetValue()/symbolTransition.GetConfidence());
							}
						}
					}
				}
			}
		}
	}

	public void UpdateConditioning(State state, Symbol symbol, float strength) {
		if (strength > 0) {
			foreach (string name in Sigma) {
				Symbol symbolA = new Symbol (name, 0.0f);
				Transition lastTransition = state.GetTransitionOn (symbol);
				Transition symbolTransition = state.GetTransitionOn (symbolA);
				if (lastInputSymbols.Contains (name)) {
					if (lastTransition.getExpectations ().DoesExpectancyExistWith (symbolTransition)) {
						float change = parameters [2] * strength / symbolTransition.GetConfidence ();
						symbolTransition.GetOutputDistribution ().UpdateSymbolProbability (lastOutput.GetName(), change);
						if (conditioned.Contains (symbolTransition)) {
							conditioned.Add (symbolTransition);
							symbolTransition.getExpectations ().AddToConfidence (parameters [2] * curSymbol.GetValue ());
							UpdateConditioning (state,symbolA,strength/symbolTransition.GetConfidence());
						}
					}
				}
				foreach (State stateA in states) {
					Transition seekingTransition = stateA.GetTransitionOn (symbolA);
					if (seekingTransition.GetEndState () == state) {
						if (lastTransition.getExpectations ().DoesExpectancyExistWith (seekingTransition)) {
							float change = parameters [2] * strength / seekingTransition.GetConfidence ();
							seekingTransition.GetOutputDistribution ().UpdateSymbolProbability (lastOutput.GetName(), change);
							if (conditioned.Contains (seekingTransition)) {
								conditioned.Add (seekingTransition);
								seekingTransition.getExpectations ().AddToConfidence (parameters [2] * curSymbol.GetValue ());
								UpdateConditioning (stateA,symbolA,strength/symbolTransition.GetConfidence());
							}
						}
					}
				}
			}
		}
	}

	public void ApplyReward() {
		float t = 1;
		Transition marked = markedDistributions.Pop ();
		while (marked != null) {
			OutputDistribution distribution = marked.GetOutputDistribution ();
			foreach (string name in markedSymbols) {
				float change = parameters [3] * t * curSymbol.GetValue () / marked.GetConfidence ();
				distribution.UpdateSymbolProbability (name, change);
				marked.AddToConfidence (parameters [3] * t * curSymbol.GetValue ());
				foreach (State state in states) {
					Transition nextTransition = state.GetTransitionOn (new Symbol(marked.GetSymbolName(),0.0f));
					if (nextTransition != null) {
						nextTransition.GetOutputDistribution ().UpdateSymbolProbability (name, change);
						nextTransition.AddToConfidence (parameters [6] * parameters [3] * t * curSymbol.GetValue ());
					}
				}
			}
			t *= parameters [7];
			marked = markedDistributions.Pop ();
		}
		markedSymbols = new List<String> ();
	}

	public void ApplyPunishment() {
		float t = 1;
		Transition marked = markedDistributions.Pop ();
		while (marked != null) {
			OutputDistribution distribution = marked.GetOutputDistribution ();
			foreach (string name in markedSymbols) {
				float change = parameters [3] * t * curSymbol.GetValue () / marked.GetConfidence ();
				distribution.UpdateSymbolProbabilityForAllBut (name, change);
				marked.AddToConfidence (parameters [3] * t * curSymbol.GetValue ());
				foreach (State state in states) {
					Transition nextTransition = state.GetTransitionOn (new Symbol (marked.GetSymbolName(), 0.0f));
					if (nextTransition != null) {
						nextTransition.GetOutputDistribution ().UpdateSymbolProbabilityForAllBut (name, change);
						nextTransition.AddToConfidence (parameters [6] * parameters [3] * t * curSymbol.GetValue ());
					}
				}
			}
			t *= parameters [7];
			marked = markedDistributions.Pop ();
		}
		markedSymbols = new List<String> ();
	}
}
