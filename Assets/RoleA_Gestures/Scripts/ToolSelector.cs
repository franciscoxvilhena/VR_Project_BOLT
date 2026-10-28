using UnityEngine;

public class ToolSelector : MonoBehaviour
{
    public GameObject scissors;
    public GameObject hammer;

    public string currentTool = "None";

    [ContextMenu("Select Scissors")]
    public void SelectScissors()
    {
        currentTool = "Scissors";
        scissors.SetActive(true);
        hammer.SetActive(false);
        Debug.Log("Selected tool: " + currentTool);
    }

    [ContextMenu("Select Hammer")]
    public void SelectHammer()
    {
        currentTool = "Hammer";
        hammer.SetActive(true);
        scissors.SetActive(false);
        Debug.Log("Selected tool: " + currentTool);
    }

    public void UseScissors()
    {
        if (currentTool == "Scissors")
        {
            Debug.Log("Using scissors");
        }
        else
        {
            Debug.Log("Scissors gesture, but scissors are not selected");
        }
    }

    public void UseHammer()
    {
        if (currentTool == "Hammer")
        {
            Debug.Log("Using hammer");
        }
        else
        {
            Debug.Log("Hammer gesture, but hammer is not selected");
        }
    }
}