using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using Random = UnityEngine.Random;

public class TheColoredPegScript : MonoBehaviour
{
	public KMBombModule module;
	public KMBombInfo bomb;
	public KMColorblindMode colourblind;
	public KMAudio sound;
	public KMSelectable buttonYes;
	public KMSelectable buttonNo;
	public TextMesh[] coloredLEDsCBT;
	public TextMesh[] pegFacesCBT;
	
	private int _moduleID;
	private static int _moduleIDCounter = 1;
	private bool _isModuleSolved;
	private int _currentStageIndex;
	private Stage _currentStage;
	private List<Stage> _stages;
	private List<List<string>> _answers;
	private List<GameObject> _stageLEDs;
	private List<GameObject> _coloredLEDs;
	private List<GameObject> _pegFaces;
	private GameObject _peg;
	private Color32 _ledOffColor;
	private Color32 _ledOnColor;
	private bool _isButtonPressed;
	private bool _isColourblindActive;

	private Dictionary<string, string> _sound = new Dictionary<string, string>()
	{
		{ "SpinSound", "rotationSound" },
		{ "ClickSound", "pressSound" },
	};

	private readonly Dictionary<string, Color> _possibleColors = new Dictionary<string, Color>()
	{
		{"R", new Color(0.9f, 0.05f, 0f)     },    // red
		{"G", new Color(0.05f, 0.9f, 0f)     },    // green
		{"B", new Color(0.05f, 0.05f, 0.9f)  },    // blue
		{"M", new Color(0.9f, 0.05f, 0.9f)   },    // magenta
		{"Y", new Color(0.9f, 0.9f, 0f)      },    // yellow
		{"C", new Color(0.05f, 0.9f, 0.9f)   },    // cyan
		{"W", new Color(0.9f, 0.9f, 0.9f)    },    // white
		{"K", new Color(0.05f, 0.05f, 0.05f) }     // black
	};
	private const string PossibleColorsStr = "RGBMYCWK";
	private float PegSpeed = 9f;
	
	const string yes = "yes";
	const string no = "no";

	public void Awake ()
	{
		_moduleID = _moduleIDCounter++;
		buttonYes.OnInteract += delegate () { ButtonPress(yes); return false; };
		buttonNo.OnInteract += delegate () { ButtonPress(no); return false; };
	}

	public void Start()
	{
		Log("Module initialization...");
		_isModuleSolved = false;
		_currentStageIndex = 0;
		_answers = new List<List<string>>();
		_peg = module.transform.Find("Peg").gameObject;
		_stages = new List<Stage>();
		_isButtonPressed = false;
		_isColourblindActive = colourblind.ColorblindModeActive;
		_stageLEDs = new List<GameObject>() {
			module.transform.Find("CounterStages").transform.Find("LedStage1").gameObject,
			module.transform.Find("CounterStages").transform.Find("LedStage2").gameObject,
			module.transform.Find("CounterStages").transform.Find("LedStage3").gameObject
		};
		_coloredLEDs = new List<GameObject>() {
			module.transform.Find("Leds").transform.Find("Led1").gameObject,
			module.transform.Find("Leds").transform.Find("Led2").gameObject,
			module.transform.Find("Leds").transform.Find("Led3").gameObject
		};
		
		_pegFaces = new List<GameObject>() {
			module.transform.Find("Peg").transform.Find("Faces").transform.Find("Face1").gameObject,
			module.transform.Find("Peg").transform.Find("Faces").transform.Find("Face2").gameObject,
			module.transform.Find("Peg").transform.Find("Faces").transform.Find("Face3").gameObject,
			module.transform.Find("Peg").transform.Find("Faces").transform.Find("Face4").gameObject,
			module.transform.Find("Peg").transform.Find("Faces").transform.Find("Face5").gameObject
		};
        
		_ledOffColor = new Color(0,0,0,0.9f);
		_ledOnColor = new Color(1,1,1,0.9f);
		
		for (int index = 0; index < _stageLEDs.Count; index++)
			_stages.Add(GenerateStage());
		

		_currentStage = _stages[_currentStageIndex];
		SetStage(_currentStage);
		UpdateStageCounter();
		Colorblind(_isColourblindActive);
		if (_isColourblindActive)
			SetColourblindMode();
		_currentStageIndex++;
		
		for (int stageCount = 0; stageCount < _stages.Count; stageCount++)
		{
			Stage stage = _stages[stageCount];
			Log(String.Format("For stage #{0}\nGenerated LEDs: {1}\nGenerated faces: {2}", 
				stageCount+1, 
				stage.LEDS(), 
				stage.Faces()));
		}

		CalculateAnswer();
		StartCoroutine(RotatePeg());
	}

	private void CalculateAnswer()
	{
		// stage 1
		List<bool> conditions = new List<bool>()
		{
			new HashSet<Color>(_stages[0].PegColors).Count == 3, // 0
			_stages[0].LEDsColors.Count(x => _stages[0].PegColors.Contains(x)) == 3, // 1
			_stages[0].PegColors.Count(x => x == _possibleColors["R"] ||  // 2
			                                x == _possibleColors["Y"] || 
			                                x == _possibleColors["M"] || 
			                                x == _possibleColors["W"]) > 2,
			bomb.IsIndicatorOn("FRQ"), // 3
			new HashSet<Color>(_stages[0].PegColors).Count == 5, // 4 
			_stages[0].LEDsColors.Count(x => x == _possibleColors["B"]) 
			+ _stages[0].PegColors.Count(x => x == _possibleColors["B"]) == 2, // 5
			bomb.GetSolvableModuleNames().Contains("Colour Flash"), //  6
			bomb.GetBatteryCount() <= 4, // 7
			bomb.GetPorts().Contains("DVI"), // 8
			Int64.Parse(bomb.GetSerialNumber().Substring(bomb.GetSerialNumber().Length-1)) % 2 == 0, // 9
			_stages[0].PegColors.Contains(_possibleColors["K"]) && _stages[0].PegColors.Contains(_possibleColors["W"]), //10
		};
		
		string answer;
		if (conditions.Count(x => x) > 4)
			answer = yes;
		else
			answer = no;
		
		_answers.Add(new List<string>(){answer, "any"});
		Log(String.Format("Stage 1: `{0}`. Total fulfilled conditions is {1}.", 
			_answers[0][0], conditions.Count(x => x)));
		
		string stringLog = "";
		for (int resultID = 0; resultID < conditions.Count; resultID++)
		{
			if (conditions[resultID])
			{
				stringLog += String.Format("Condition {0} is true\n", resultID + 1);
			}
		}
		Log(stringLog);
		
		// stage 2
		List<Color> bothStagesColors = new List<Color>();
		bothStagesColors.AddRange(_stages[0].LEDsColors);
		bothStagesColors.AddRange(_stages[0].PegColors);
		bothStagesColors.AddRange(_stages[1].LEDsColors);
		bothStagesColors.AddRange(_stages[1].PegColors);

		List<List<int>> table = new List<List<int>>()
		{
			new List<int> { 5, 1, 4, 2, 9, 7, 8, 3, 6 },
			new List<int> { 3, 4, 2, 9, 1, 9, 7, 7, 5 },
			new List<int> { 2, 7, 1, 7, 5, 6, 3, 1, 7 },
			new List<int> { 8, 6, 8, 8, 3, 1, 1, 6, 9 },
			new List<int> { 7, 2, 9, 5, 4, 3, 4, 5, 1 },
			new List<int> { 4, 3, 7, 3, 8, 4, 5, 2, 2 },
			new List<int> { 6, 8, 6, 4, 7, 5, 2, 9, 3 },
			new List<int> { 1, 9, 3, 6, 2, 8, 6, 8, 4 },
			new List<int> { 9, 5, 5, 1, 6, 2, 9, 4, 5 }
		};
		int matrixSize = table.Count;
		int countRed = bothStagesColors.Count(x => x.r > 0.05f);
		int countGreen = bothStagesColors.Count(x => x.g > 0.05f);
		int countBlue = bothStagesColors.Count(x => x.b > 0.05f);
		int value = table[countRed%matrixSize][countGreen%matrixSize] * countBlue % (matrixSize * matrixSize);
		if (table[(value/matrixSize)][value % matrixSize] > 6)
			answer = yes;
		else
			answer = no;
		
		_answers.Add(new List<string>(){answer, String.Format("{0}", countRed%9)});
		Log(String.Format("Stage 2: `{0}` when last digit timer is {1}. Count colors: R{2} G{3} B{4}", 
			_answers[1][0], _answers[1][1], countRed, countGreen, countBlue));
		
		Dictionary<Color, int> numbers = new Dictionary<Color, int>()
		{
			{new Color(0.9f, 0.05f, 0f)     , Convert.ToInt32("10110", 2) },
			{new Color(0.05f, 0.9f, 0f)     , Convert.ToInt32("11111", 2) },
			{new Color(0.05f, 0.05f, 0.9f)  , Convert.ToInt32("00010", 2) },
			{new Color(0.9f, 0.05f, 0.9f)   , Convert.ToInt32("10001", 2) },
			{new Color(0.9f, 0.9f, 0f)      , Convert.ToInt32("10111", 2) },
			{new Color(0.05f, 0.9f, 0.9f)   , Convert.ToInt32("11010", 2) },
			{new Color(0.9f, 0.9f, 0.9f)    , Convert.ToInt32("11100", 2) },
			{new Color(0.05f, 0.05f, 0.05f) , Convert.ToInt32("01101", 2) }
		};
		Dictionary<Color, Func<int, int, int>> operators = new Dictionary<Color, Func<int, int, int>>()
		{
			{new Color(0.9f, 0.05f, 0f)    , OR }, // OR
			{new Color(0.05f, 0.9f, 0f)    , AND }, // AND
			{new Color(0.05f, 0.05f, 0.9f) , XOR }, // XOR
			{new Color(0.9f, 0.05f, 0.9f)  , IMPL }, // Implication
			{new Color(0.9f, 0.9f, 0f)     , NAND }, // NAND
			{new Color(0.05f, 0.9f, 0.9f)  , NOR }, // NOR
			{new Color(0.9f, 0.9f, 0.9f)   , XNOR }, // XNOR
			{new Color(0.05f, 0.05f, 0.05f), AND }, // AND
		};
		Stage stage = _stages[2];
		int A = numbers[stage.LEDsColors[0]];
		int B = numbers[stage.LEDsColors[1]];
		int C = numbers[stage.LEDsColors[2]];
		
		int first = operators[stage.PegColors[3]](operators[stage.PegColors[0]](A, operators[stage.PegColors[1]](B, C)), C);
		int second = operators[stage.PegColors[2]](B, operators[stage.PegColors[3]](C, operators[stage.PegColors[4]](A, C)));
		if (first >= second)
			_answers.Add(new List<string>(){yes, String.Format("{0}", mod(second, 10))});
		else
			_answers.Add(new List<string>(){no, String.Format("{0}", mod(first, 10))});
		
		Log(String.Format("Stage 3: `{0}` when last digit timer is {1}.", 
			_answers[2][0], _answers[2][1]));
	}

	private void ButtonPress(string pressed)
	{
		if (_isButtonPressed) return;
		_isButtonPressed = true;
		int lastSecond = GetBombLastSecond();
		StartCoroutine(ButtonPressAnimation(pressed));
		if (_isModuleSolved) return;
		if (pressed.Equals(yes))
		{
			buttonYes.AddInteractionPunch(0.25f);
		}
		else
		{
			buttonNo.AddInteractionPunch(0.25f);
		}
		if (_answers[_currentStageIndex - 1][0] != pressed 
		    || (_currentStageIndex != 1 && lastSecond != _answers[_currentStageIndex - 1][1].TryParseInt()))
		{
			module.HandleStrike();
			Log(String.Format("Wrong button! Expected: {0} when last digit is {1}. Pressed {2} when last digit is {3}", 
				_answers[_currentStageIndex-1][0], _answers[_currentStageIndex-1][1], pressed, lastSecond));
			return;
		}
		StartCoroutine(ButtonPressedUtils());
	}

	private IEnumerator ButtonPressedUtils()
	{
		UpdateStageCounter();
		StartCoroutine(StageChangeAnimation());
		yield return new WaitForSeconds(1.7f);
		if (_currentStageIndex == _stages.Count)
		{
			Solve();
			Colorblind(false);
			yield break;
		}

		_currentStage = _stages[_currentStageIndex];
		SetStage(_currentStage);
		if (_isColourblindActive)
		{
			SetColourblindMode();
		}
		_currentStageIndex++;
	}

	private void Colorblind(bool status)
	{
		foreach (var text in coloredLEDsCBT)
		{
			text.gameObject.SetActive(status);
		}
		foreach (var text in pegFacesCBT)
		{
			text.gameObject.SetActive(status);
		}
	}

	private void SetColourblindMode()
	{
		for (int i = 0; i < coloredLEDsCBT.Length; i++)
		{
			coloredLEDsCBT[i].text = Stage.GetColorNameByColor(_currentStage.LEDsColors[i]);
		}
		for (int i = 0; i < pegFacesCBT.Length; i++)
		{
			pegFacesCBT[i].text = Stage.GetColorNameByColor(_currentStage.PegColors[i]);
		}
	}

	private void SetStage(Stage stage)
	{
		for (int index = 0; index < _coloredLEDs.Count; index++)
		{
			_coloredLEDs[index].GetComponent<Renderer>().material.color = stage.LEDsColors[index];
		}
		for (int index = 0; index < _pegFaces.Count; index++)
		{
			_pegFaces[index].GetComponent<Renderer>().material.color = stage.PegColors[index];
		}
	}

	private void UpdateStageCounter()
	{
		for (var index = 0; index < _stageLEDs.Count; index++)
		{
			if (index < _currentStageIndex)
				_stageLEDs[index].GetComponent<Renderer>().material.color = _ledOnColor;
			else
				_stageLEDs[index].GetComponent<Renderer>().material.color = _ledOffColor;
		}
	}

	private IEnumerator StageChangeAnimation()
	{
		float speedCoefficient = 250f;
		sound.PlaySoundAtTransform(_sound["SpinSound"], module.transform);
		PegSpeed = -PegSpeed/speedCoefficient;
		yield return new WaitForSeconds(1.75f);
		PegSpeed = -PegSpeed*speedCoefficient;
		yield return null;
	}

	private IEnumerator RotatePeg()
	{
		float t = 0;
		while (true)
		{
			yield return null;
			t += Time.deltaTime;
			_peg.transform.localEulerAngles = new Vector3(_peg.transform.localEulerAngles.x, 
				_peg.transform.localEulerAngles.y, 
				(t * 360)  / PegSpeed);
		} 
	}

	private IEnumerator ButtonPressAnimation(string pressed)
	{
		Vector3 vectorChanging = new Vector3(0f, 0.002f, 0f);
		KMSelectable button = pressed.Equals(yes) ? buttonYes : buttonNo;
		button.transform.localPosition -= vectorChanging;
		yield return new WaitForSeconds(0.5f);
		button.transform.localPosition += vectorChanging;
		_isButtonPressed = false;
	}

	private Color ChooseColor()
	{
		return GetColor(PossibleColorsStr[Random.Range(0, PossibleColorsStr.Length)].ToString());
	}

	private Color GetColor(string color)
	{
		return _possibleColors[color];
	}

	private Stage GenerateStage()
	{
		Stage stage = new Stage();
		for (int index = 0; index < _coloredLEDs.Count; index++)
		{
			stage.AddLedColor(ChooseColor());
		}
		for (int index = 0; index < _pegFaces.Count; index++)
		{
			stage.AddPegColor(ChooseColor());
		}
		return stage;
	}

	int mod(int x, int m) {
		return (x%m + m)%m;
	}

	private int XOR(int a, int b)
	{
		return a ^ b;
	}

	private int AND(int a, int b)
	{
		return a & b;
	}

	private int OR(int a, int b)
	{
		return a | b;
	}

	private int NOR(int a, int b)
	{
		return mod(~(a | b), 32);
	}

	private int NAND(int a, int b)
	{
		return mod(~(a & b), 32);
	}
	private int IMPL(int a, int b)
	{
		return mod(a & (~b), 32);
	}

	private int XNOR(int a, int b)
	{
		return mod(~(a ^ b), 32);
	}

	private void Incorrect()
	{
		module.HandleStrike();
	}

	private void Solve()
	{
		_isModuleSolved = true;
		module.HandlePass();
	}
	
	private void Log(string logString)
	{
		foreach (var logLine in logString.Split('\n'))
			Debug.Log(String.Format("[The Colored Peg #{0}] {1}", _moduleID, logLine));
	}

	private int GetBombLastSecond()
	{
		return bomb.GetFormattedTime()[bomb.GetFormattedTime().Length - 1] - '0';
	}
	
#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Use !{0} yes/no at [time] to press button yes or no at specific time.";
#pragma warning restore 414

	IEnumerator ProcessTwitchCommand (string Command)
	{
		string[] parameters = Command.Split(' ');
		if (parameters.Length < 3)
		{
			yield return "sendtochaterror Command has less than 3 arguments.";
			yield break;
		}
		if (!(parameters[2].TryParseInt() >= 0))
		{
			yield return "sendtochaterror Time is not in correct format.";
			yield break;
		}
		
		int time = int.Parse(parameters[2]);
		
		if (Command.StartsWith(yes))
		{
			yield return null;
			int lastSecond = GetBombLastSecond();
			while (lastSecond != time)
			{
				lastSecond = GetBombLastSecond();
				yield return new WaitForSeconds(0.5f);
			}
			
			buttonYes.OnInteract();
			yield break;
		}
		
		if (Command.StartsWith(no))
		{
			yield return null;
			int lastSecond = GetBombLastSecond();
			while (lastSecond != time)
			{
				lastSecond = GetBombLastSecond();
				yield return new WaitForSeconds(0.5f);
			}
			buttonNo.OnInteract();
			yield break;
		}

		yield return "sendtochaterror Command format is wrong!";
	}

	IEnumerator TwitchHandleForcedSolve () {
		Solve();
		Log("Module force-solved");
		yield return null;
	}
}
