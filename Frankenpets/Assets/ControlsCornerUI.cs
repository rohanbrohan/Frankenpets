using UnityEngine;

public class ControlsCornerUI : MonoBehaviour
{
    [Header("Corner/mini Controls")]
    public GameObject cornerControlsUIParent;
    private GameObject cornerControlsUI;
    private GameObject P1ControlsCF;
    private GameObject P1ControlsCB;
    private GameObject P2ControlsDF;
    private GameObject P2ControlsDB;

    [Header("Script references")]
    public ControllerAssignment controllerAssignment;
    public PlayerManager playerManager;

    [Header("Show the controls corner?")]
    public bool show = false; 

    void Awake()
    {
        // controllerAssignment = FindObjectOfType<ControllerAssignment>();
        // playerManager = FindObjectOfType<PlayerManager>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (controllerAssignment.IsKeyboard()) cornerControlsUI = cornerControlsUIParent.transform.GetChild(0).gameObject;
        else cornerControlsUI = cornerControlsUIParent.transform.GetChild(1).gameObject;

        if (show) cornerControlsUI.SetActive(true);
        
        Transform P1Controls = cornerControlsUI.transform.GetChild(0).gameObject.transform;
        Transform P2Controls = cornerControlsUI.transform.GetChild(1).gameObject.transform;

        P1ControlsCF = P1Controls.GetChild(0).gameObject;
        P1ControlsCB = P1Controls.GetChild(1).gameObject;

        P2ControlsDF = P2Controls.GetChild(0).gameObject;
        P2ControlsDB = P2Controls.GetChild(1).gameObject;

    }

    // Update is called once per frame
    void Update()
    {
        if (show) cornerControlsUI.SetActive(true);
        updateControlsCornerUI();
    }

    public void updateControlsCornerUI()
    {
        // bool isP1Cat = playerManager.P1.Species == "cat";
        bool isP1Front = playerManager.P1.IsFront;

        P1ControlsCF.SetActive(isP1Front);
        P1ControlsCB.SetActive(!isP1Front);

        P2ControlsDF.SetActive(!isP1Front);
        P2ControlsDB.SetActive(isP1Front);
    }

    public void setShow(bool setting)
    {
        show = setting;
    }
}
