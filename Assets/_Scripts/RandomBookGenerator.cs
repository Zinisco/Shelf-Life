using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Drawing;

public class RandomBookGenerator : MonoBehaviour
{
    [SerializeField]
    private TMP_Text CoverTitle_Text;
    [SerializeField]
    private TMP_Text SpineName_Text;

    [SerializeField]
    String[] GenreNames;
    [SerializeField]
    String[] RomanceAdjectiveWord;
    [SerializeField]
    String[] RomanceNounWord;
    [SerializeField]
    String[] RomancePluralNounWord;
    [SerializeField]
    String[] MysteryAdjectiveWord;
    [SerializeField]
    String[] MysteryNounWord;
    [SerializeField]
    String[] MysteryPluralNounWord;
    [SerializeField]
    String[] SciFiAdjectiveWord;
    [SerializeField]
    String[] SciFiNounWord;
    [SerializeField]
    String[] FantasyAdjectiveWord;
    [SerializeField]
    String[] FantasyNounWord;
    [SerializeField]
    String[] HorrorAdjectiveWord;
    [SerializeField]
    String[] HorrorNounWord;

    private MeshRenderer m_Renderer;


    private void Awake()
    {
        m_Renderer = GetComponent<MeshRenderer>();
    }


    private void Start()
    {
        ChooseGenre();
    }

    private void ChooseGenre()
    {
        int randomIndex = UnityEngine.Random.Range(0, GenreNames.Length);
        string randomGenre = GenreNames[randomIndex];

        switch (randomGenre)
        {
            case "Romance":
                CreateRomanceBookTitle();
                break;
            case "Mystery":
                CreateMysteryBookTitle();
                break;
            case "SciFi":
                CreateSciFiBookTitle();
                break;
            case "Fantasy":
                CreateFantasyBookTitle();
                break;
            case "Horror":
                CreateHorrorBookTitle();
                break;
            default:
                Debug.Log("No genre selected");
                break;
        }
    }

    private void CreateRomanceBookTitle()
    {
        string randomTitleName = "";
        string randomSpineName = "";

        int randomAdjectiveIndex = UnityEngine.Random.Range(0, RomanceAdjectiveWord.Length);
        int randomAdjectiveIndex2;

        int randomNounIndex = UnityEngine.Random.Range(0, RomanceNounWord.Length);
        int randomPluralNounIndex = UnityEngine.Random.Range(0, RomancePluralNounWord.Length);
        int randomNounIndex2;

        int randomTemplateIndex = UnityEngine.Random.Range(0, 3);

        //prevent duplicates
        do
        {
            randomAdjectiveIndex2 = UnityEngine.Random.Range(0, RomanceAdjectiveWord.Length);
        }
        while (randomAdjectiveIndex == randomAdjectiveIndex2);

        do
        {
            randomNounIndex2 = UnityEngine.Random.Range(0, RomanceAdjectiveWord.Length);
        }
        while (randomNounIndex == randomNounIndex2);

        // Step 3: Get the words at the random index
        string randomAdjective = RomanceAdjectiveWord[randomAdjectiveIndex];
        string randomAdjective2 = RomanceAdjectiveWord[randomAdjectiveIndex2];
        string randomNoun = RomanceNounWord[randomNounIndex];
        string randomNoun2 = RomanceNounWord[randomNounIndex2];
        string randomPluralNoun = RomancePluralNounWord[randomPluralNounIndex];

        switch (randomTemplateIndex)
        {
            case 0:
                randomTitleName = "The \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "The " + randomAdjective + " " + randomNoun;
                break;
            case 1:
                randomTitleName = randomAdjective + "\n Hearts \n & \n" + randomAdjective2 + " Souls";
                randomSpineName = randomAdjective + " Hearts & " + randomAdjective2 + " Souls";
                break;
            case 2:
                randomTitleName = randomNoun + "\n of the \n" + randomAdjective;
                randomSpineName = randomNoun + " of the " + randomAdjective;
                break;
               
        }

        //Update the TextMeshPro UI element
        CoverTitle_Text.text = randomTitleName;

        SpineName_Text.text = randomSpineName;

        ChooseRandomBrightColor();
    }
        private void CreateMysteryBookTitle()
    {
        string randomTitleName = "";
        string randomSpineName = "";

        int randomAdjectiveIndex = UnityEngine.Random.Range(0, MysteryAdjectiveWord.Length);
        int randomNounIndex = UnityEngine.Random.Range(0, MysteryNounWord.Length);
        int randomPluralNounIndex = UnityEngine.Random.Range(0, MysteryPluralNounWord.Length);
        int randomTemplateIndex = UnityEngine.Random.Range(0, 3);

        //Get the word at the random index
        string randomAdjective = MysteryAdjectiveWord[randomAdjectiveIndex];
        string randomNoun = MysteryNounWord[randomNounIndex];
        string randomPluralNoun = MysteryPluralNounWord[randomPluralNounIndex];


        switch (randomTemplateIndex)
        {
            case 0:
                randomTitleName = "The \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "The " + randomAdjective + " " + randomNoun;
                break;
            case 1:
                randomTitleName = randomPluralNoun + "\n of the \n" + randomAdjective;
                randomSpineName = randomPluralNoun + " of the " + randomAdjective;
                break;
            case 2:
                randomTitleName = "A \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "A " + randomAdjective + " " + randomNoun;
                break;
            case 3:
                randomTitleName = "The \n" + randomNoun + " \n of \n " + randomAdjective + "Crimes ";
                randomSpineName = "The " + randomNoun + " of " + randomAdjective + "Crimes ";
                break;
        }

        //Update the TextMeshPro UI element
        CoverTitle_Text.text = randomTitleName;

        SpineName_Text.text = randomSpineName;

        ChooseRandomDarkColor();
    }

    private void CreateSciFiBookTitle()
    {
        string randomTitleName = "";
        string randomSpineName = "";

        int randomAdjectiveIndex = UnityEngine.Random.Range(0, SciFiAdjectiveWord.Length);
        int randomNounIndex = UnityEngine.Random.Range(0, SciFiNounWord.Length);
        int randomTemplateIndex = UnityEngine.Random.Range(0, 1);

        //Get the word at the random index
        string randomAdjective = SciFiAdjectiveWord[randomAdjectiveIndex];
        string randomNoun = SciFiNounWord[randomNounIndex];

        switch (randomTemplateIndex)
        {
            case 0:
                randomTitleName = "The \n" + randomAdjective + " " + randomNoun;
                randomSpineName = "The " + randomAdjective + " " + randomNoun;
                break;
            case 1:
                randomTitleName = "A \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "A " + randomAdjective + " " + randomNoun;
                break;
            case 2:
                randomTitleName = "The \n" + randomNoun + " \n of the \n " + randomAdjective;
                randomSpineName = "The " + randomNoun + " of the " + randomAdjective;
                break;
        }

        //Update the TextMeshPro UI element
        CoverTitle_Text.text = randomTitleName;

        SpineName_Text.text = randomSpineName;

        ChooseRandomDarkColor();
    }

    private void CreateFantasyBookTitle()
    {
        string randomTitleName = "";
        string randomSpineName = "";

        int randomAdjectiveIndex = UnityEngine.Random.Range(0, FantasyAdjectiveWord.Length);
        int randomNounIndex = UnityEngine.Random.Range(0, FantasyNounWord.Length);
        int randomTemplateIndex = UnityEngine.Random.Range(0, 2);

        //Get the word at the random index
        string randomAdjective = FantasyAdjectiveWord[randomAdjectiveIndex];
        string randomNoun = FantasyNounWord[randomNounIndex];

        switch (randomTemplateIndex)
        {
            case 0:
                randomTitleName = "The \n" + randomAdjective + " " + randomNoun;
                randomSpineName = "The " + randomAdjective + " " + randomNoun;
                break;
            case 1:
                randomTitleName = "A \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "A " + randomAdjective + " " + randomNoun;
                break;
            case 2:
                randomTitleName = "The \n" + randomNoun + " \n of the \n " + randomAdjective;
                randomSpineName = "The " + randomNoun + " of the " + randomAdjective;
                break;
        }

        //Update the TextMeshPro UI element
        CoverTitle_Text.text = randomTitleName;

        SpineName_Text.text = randomSpineName;

        ChooseRandomDarkColor();
    }

    private void CreateHorrorBookTitle()
    {
        string randomTitleName = "";
        string randomSpineName = "";

        int randomAdjectiveIndex = UnityEngine.Random.Range(0, HorrorAdjectiveWord.Length);
        int randomNounIndex = UnityEngine.Random.Range(0, HorrorNounWord.Length);
        int randomTemplateIndex = UnityEngine.Random.Range(0, 3);

        //Get the word at the random index
        string randomAdjective = HorrorAdjectiveWord[randomAdjectiveIndex];
        string randomNoun = HorrorNounWord[randomNounIndex];

        switch (randomTemplateIndex)
        {
            case 0:
                randomTitleName = "The \n" + randomAdjective + " " + randomNoun;
                randomSpineName = "The " + randomAdjective + " " + randomNoun;
                break;
            case 1:
                randomTitleName = "A \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "A " + randomAdjective + " " + randomNoun;
                break;
            case 2:
                randomTitleName = "Curse of the \n" + randomAdjective + " \n " + randomNoun;
                randomSpineName = "Curse of the " + randomAdjective + " " + randomNoun;
                break;
            case 3:
                randomTitleName = "The \n" + randomNoun + " \n of the \n " + randomAdjective;
                randomSpineName = "The " + randomNoun + " of the " + randomAdjective;
                break;
        }

        //Update the TextMeshPro UI element
        CoverTitle_Text.text = randomTitleName;

        SpineName_Text.text = randomSpineName;

        ChooseRandomDarkColor();
    }

    private void ChooseRandomBrightColor()
    {
        string[] colorNames = { "SkyBlue", "SunsentOrange", "Purple", "LimeGreen", "Red"};

        // Pick a random color
        int randomColorIndex = UnityEngine.Random.Range(0, colorNames.Length);
        string selectedColor = colorNames[randomColorIndex];

        UnityEngine.Color newColor;

        switch (selectedColor)
        {
            case "SkyBlue":
                newColor = new UnityEngine.Color(0.1f, 0.1f, 0.92f);  // RGB for DarkBlue
                break;
            case "Magneta":
                newColor = UnityEngine.Color.magenta;
                break;
            case "Red":
                newColor = UnityEngine.Color.red;
                break;
            case "SunsetOrange":
                newColor = new UnityEngine.Color(0.85f, 0.58f, 0.09f);  // RGB for SunsetOrange
                break;
            case "Purple":
                newColor = new UnityEngine.Color(0.77f, 0.09f, 0.85f);  // RGB for Purple
                break;
            case "LimeGreen":
                newColor = new UnityEngine.Color(0.29f, 0.91f, 0.27f);  // RGB for LimeGreen
                break;
            default:
                newColor = UnityEngine.Color.white; // Default color (if the string doesn't match any cases)
                break;
        }

        m_Renderer.material.color = newColor;
    }

    private void ChooseRandomDarkColor()
    {
        string[] colorNames = { "DarkBlue", "Black", "Red", "Green", "Grey" };

        // Pick a random color
        int randomColorIndex = UnityEngine.Random.Range(0, colorNames.Length);
        string selectedColor = colorNames[randomColorIndex];

        UnityEngine.Color newColor;

        switch (selectedColor)
        {
            case "DarkBlue":
                newColor = new UnityEngine.Color(0.1f, 0.1f, 0.92f);  // RGB for DarkBlue
                break;
            case "Red":
                newColor = UnityEngine.Color.red;
                break;
            case "Black":
                newColor = UnityEngine.Color.black;  // RGB for Black
                break;
            case "Green":
                newColor = UnityEngine.Color.green;  // RGB for Black
                break;
            case "Grey":
                newColor = UnityEngine.Color.grey;  // RGB for Grey
                break;
            default:
                newColor = UnityEngine.Color.white; // Default color (if the string doesn't match any cases)
                break;
        }

        m_Renderer.material.color = newColor;
    }
}
