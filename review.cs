using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;
using VInspector;
using Redstoneinvente.HTMLBuilder;

using ContextMenu = UnityEngine.ContextMenu;
using LoginEmail = RedsFirebaseAuth.LoginEmail;
using UpdateUserProfileResponse = RedsFirebaseAuth.LoginManager.UpdateUserProfileResponse;
using UserInfo = RedsFirebaseAuth.LoginManager.UserInfo;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager networkManager;

    public delegate void OnDBFetched();
    public static OnDBFetched onDBFetched;

    [SerializeField] string databaseURL = $"https://fscproject-c108d-default-rtdb.asia-southeast1.firebasedatabase.app/[PATH].json?auth=[AUTH]";
    [SerializeField] string storageURL = $"https://firebasestorage.googleapis.com/v0/b/fscproject-c108d.appspot.com/o/[NAME].png?alt=media";

    public static string getDBURL
    {
        get { return networkManager.databaseURL; }
    }

    public static string getStorageURL
    {
        get { return networkManager.storageURL; }
    }

#nullable enable
    public Database? database;
#nullable disable

    [SerializeField] GameObject eventListItemPrefabs;
    [SerializeField] Transform eventListItemPrefabsTransform;
    [SerializeField] List<EventListItemManager> eventList;
    [SerializeField] List<GameObject> eventListObjects;

    public int sortingDepth = 5;

    [SerializeField] int onGoingWeight = 1;
    [SerializeField] int upcommingWeight = 10;
    [SerializeField] int pastWeight = 20;

    public List<GameObject> transformObjs = new List<GameObject>();

    [SerializeField] GameObject header;
    [SerializeField] List<GameObject> headerList;

    [SerializeField] string ongoingName;
    [SerializeField] string pastName;
    [SerializeField] string upcomignName;

    [SerializeField] bool useAuthenticityCheck;

    [SerializeField] URLFormater firebaseLoginURLFormater;
    [SerializeField] RedsFirebaseAuth redsFirebaseAuth;

    [SerializeField] UpdateUserProfileResponse redsUpdatedProfile;
    //[SerializeField] UserInfo redsUserInfo;

    public DatabaseSecurityParameters databaseSecurityParameters;

    public UserInfo testingInfo;

    [Header("Testing")]

    public string emailTest = "pgowardun@gmail.com";
    public string password = "Pranav12345";

    public string testUID;
    public float points;

    public bool useTestReward;

    public string askLoginStateName = "Login";
    public string loginInStateName = "login";
    public string loginInFaliedStateName = "login failed";
    public string noEventStateName = "no events";

    private void Awake()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        networkManager = this;

        eventList = new List<EventListItemManager>();
        eventListObjects = new List<GameObject>();

        QRScanner.onScannedSuccess += OnScanned;

        if (PlayerPrefs.HasKey("auth"))
        {
            RedsVectorStatesManager.ShowVector(loginInStateName);
            RedsVectorStatesManager.HideVector(askLoginStateName);

            LoginEmail loginEmail = new LoginEmail();
            redsFirebaseAuth.AutoLogin(loginEmail, ref firebaseLoginURLFormater.auth, firebaseLoginURLFormater, databaseSecurityParameters.forceRenewToken);
        }

        StartCoroutine(GetInitial());

        RedsLeaderboardSystem.onRewardPoints += RewardCurrentUser;
    }

    public void OnScanned(QRData data)
    {
        if (!data.IsQRDataAuthentic() && useAuthenticityCheck)
        {
            Debug.LogWarning("QR Code Not Authentic!");
            return;
        }

        database.GetEvent(data.eventID).MarkUserPresent(data.personID, true);
        Debug.Log($"Marked Present! user id: {data.personID}, event : {data.eventID}");
        UpdateEventDB();

        foreach (var item in eventList)
        {
            if (item.data.eventID == data.eventID)
            {
                item.Initialize(item.data);
            }
        }
    }

    /// <summary>
    /// Uses pattern recognition to "guess" when the next event might be
    /// </summary>
    /// <returns></returns>
    public static KeyValuePair<DateTime, DateTime> GetProbableStartEndDate()
    {
        EventManager[] evnts = networkManager.database.events;

        float durationBetweenCreationAndStart = -1;
        float durationOfEvent = -1;

        int startHour = -1;
        int startMinute = -1;

        int endHour = -1;
        int endMinute = -1;

        foreach (var event_ in evnts)
        {
            bool dontAvergae = durationBetweenCreationAndStart == -1;

            DateTime start = DateTime.Parse(event_.startDate);
            DateTime created = DateTime.Parse(event_.createdDate);
            DateTime end = DateTime.Parse(event_.endDate);

            int daysDelay = (int)(start - created).TotalDays;

            int daysDelayEvent = (int)(end - start).TotalDays;

            if (dontAvergae)
            {
                durationBetweenCreationAndStart = daysDelay;

                durationOfEvent = daysDelayEvent;

                startHour = start.Hour;
                startMinute = start.Minute;

                endHour = end.Hour;
                endMinute = end.Minute;
            }
            else
            {
                durationBetweenCreationAndStart += daysDelay;
                durationBetweenCreationAndStart /= 2;

                durationOfEvent += daysDelayEvent;
                durationOfEvent /= 2;

                startHour += start.Hour;
                startHour /= 2;

                startMinute += start.Minute;
                startMinute /= 2;

                endHour += end.Hour;
                endHour /= 2;

                endMinute += end.Minute;
                endMinute /= 2;
            }
        }

        DateTime startTemp = DateTime.Today.AddDays(durationBetweenCreationAndStart);
        DateTime startDate = new DateTime(startTemp.Year, startTemp.Month, startTemp.Day, startHour, startMinute, 0);

        DateTime endTemp = startDate.AddDays(durationOfEvent);
        DateTime endDate = new DateTime(endTemp.Year, endTemp.Month, endTemp.Day, endHour, endMinute, 0);

        return new KeyValuePair<DateTime, DateTime>(startDate, endDate);
    }

    void OnDestroy()
    {
        RedsLeaderboardSystem.onRewardPoints -= RewardCurrentUser;
    }

    [Button]
    public void TestFirebaseLogin()
    {
        LoginEmail loginEmail = new LoginEmail();

        redsFirebaseAuth.SignIn(loginEmail, emailTest, password, firebaseLoginURLFormater.auth.IsExpired(), firebaseLoginURLFormater.auth.refreshToken, firebaseLoginURLFormater, true);
    }

    [Button]
    public void TestFirebaseSingup()
    {
        LoginEmail loginEmail = new LoginEmail();

        redsFirebaseAuth.SignUp(loginEmail, emailTest, password, false, "", firebaseLoginURLFormater);
    }

    [Button]
    public void TestUpdateUsername()
    {
        LoginEmail loginEmail = new LoginEmail();

        redsFirebaseAuth.UpdateProfile(loginEmail, firebaseLoginURLFormater.auth.token, "Pranav", "", redsUpdatedProfile);
    }

    [Button]
    public void TestFetchUserName()
    {
        LoginEmail loginEmail = new LoginEmail();

        redsFirebaseAuth.GetProfile(loginEmail, firebaseLoginURLFormater.auth.token, redsFirebaseAuth.redsUserInfo, false);
    }

    [Button]
    public void TestLogout()
    {
        PlayerPrefs.DeleteKey("auth");
    }

    [Button]
    public void CreateUser()
    {
        AddUser(testingInfo, new User());
    }

    public void CreateUser(UserInfo userInfo, User user)
    {
        AddUser(userInfo, user);
    }

    [Button]
    [ContextMenu("Test QR")]
    public void TestQR()
    {
        database.users[0].GetQRCode("Test Event");
    }

    [ContextMenu("Add Ongoing Event")]
    public void AddOngoingEventTest()
    {
        AddEvent(new EventManager("1", "Event A", "Event A", "Event A", "Mauritius Standard Time", DateTime.Now.AddDays(-1).ToString("dd MMMM yyyy"), DateTime.Now.AddDays(1).ToString("dd MMMM yyyy"), new List<string>(), new List<string>(), "1", 1, 1, new EventManager.Session[0], new string[0], "", "", new EventManager.SerializableHCT(0, 0, 0, false), new EventImageTemplate("", "", new EventImageTemplate.EventImageDBProperties[0], new EventImageTemplate.EventImageDBProperties[0])), default);
    }

    [ContextMenu("Add Over Event")]
    public void AddOverEventTest()
    {
        AddEvent(new EventManager("1", "Event A", "Event A", "Event A", "Mauritius Standard Time", DateTime.Now.AddDays(-3).ToString("dd MMMM yyyy"), DateTime.Now.AddDays(-1).ToString("dd MMMM yyyy"), new List<string>(), new List<string>(), "1", 1, 1, new EventManager.Session[0], new string[0], "", "", new EventManager.SerializableHCT(0, 0, 0, false), new EventImageTemplate("", "", new EventImageTemplate.EventImageDBProperties[0], new EventImageTemplate.EventImageDBProperties[0])), default);
    }

    [Button()]
    public void TestAddPts()
    {
        database.events[0].AddPointsToUser(testUID, points);
    }

    public void RewardCurrentUser(float amount)
    {
        if (useTestReward)
        {
            EventJoiningManager.currentEvent = database.events[0];
            EventJoiningManager.currentEvent.AddPointsToUser(EventJoiningManager.currentEvent.attendees[0], amount);
        }
        else
        {
            try
            {
                EventJoiningManager.currentEvent.AddPointsToUser(redsFirebaseAuth.redsUserInfo.localId, amount);
            }
            catch (Exception ex)
            {
                EventJoiningManager.currentEvent = database.events[0];
                EventJoiningManager.currentEvent.AddPointsToUser(redsFirebaseAuth.redsUserInfo.localId, amount);
            }
        }
    }

    public void AddUser(User user)
    {
        database.AddUser(user);

        UpdateUsersDB();
    }

    public void AddUser(UserInfo user, User userC)
    {
        //Create user
        LoginEmail loginEmail = new LoginEmail();

        redsFirebaseAuth.SignUp(loginEmail, user, userC);
    }

    public void UploadToDB(string path, string json)
    {
        StartCoroutine(UploadToDBC(path, json));
    }

    IEnumerator UploadToDBC(string path, string json)
    {
        UnityWebRequest req = UnityWebRequest.Put(redsFirebaseAuth.AuthenticateURL(databaseURL.Replace("[PATH]", path), databaseSecurityParameters.requireAuthenticationForDB), json);

        yield return req.SendWebRequest();

        Debug.Log("Done!" + req.error);
    }

    public GameObject AddEvent(EventManager thisEventT, Texture texture)
    {
        GameObject ev = AddVisualEventGetObj(thisEventT, texture);

        RedsVectorStatesManager.HideVector(noEventStateName);

        database.AddEvent(thisEventT);
        thisEventT.eventID = database.events.Length + "";

        UploadToDB("database", JsonUtility.ToJson(database));

        return ev;
    }

    public void UpdateEventDB()
    {
        UploadToDB("database", JsonUtility.ToJson(database));
    }

    public void UpdateUsersDB()
    {
        UploadToDB("database", JsonUtility.ToJson(database));
    }

    public void AddVisualEvent(EventManager thisEventT, Texture texture)
    {
        GameObject ev = Instantiate(eventListItemPrefabs, eventListItemPrefabsTransform);
        EventListItemManager thisEvent = ev.GetComponent<EventListItemManager>();
        thisEvent.Initialize(thisEventT, texture);

        ev.name = GetWeightedValue(thisEventT) + "";

        eventList.Add(thisEvent);
        eventListObjects.Add(ev);

        //AddVisualEventGetObj(thisEventT, texture);
    }

    public void AddVisualEvent(EventManager thisEventT, Texture2D texture)
    {
        GameObject ev = Instantiate(eventListItemPrefabs, eventListItemPrefabsTransform);
        EventListItemManager thisEvent = ev.GetComponent<EventListItemManager>();
        thisEvent.Initialize(thisEventT, texture);

        ev.name = GetWeightedValue(thisEventT) + "";

        eventList.Add(thisEvent);
        eventListObjects.Add(ev);
        //AddVisualEventGetObj(thisEventT, texture);
    }

    public GameObject AddVisualEventGetObj(EventManager thisEventT, Texture texture)
    {
        GameObject ev = Instantiate(eventListItemPrefabs, eventListItemPrefabsTransform);
        EventListItemManager thisEvent = ev.GetComponent<EventListItemManager>();
        thisEvent.Initialize(thisEventT, texture);

        ev.name = GetWeightedValue(thisEventT) + "";

        eventList.Add(thisEvent);
        eventListObjects.Add(ev);

        SortEvents();

        return ev;
    }

    public float GetWeightedValue(EventManager eventManager)
    {
        int weight = eventManager.IsOngoing() ? onGoingWeight : eventManager.IsPast() ? pastWeight : upcommingWeight;

        return (eventManager.ElapsedDays() == 0 ? 1 : eventManager.ElapsedDays()) * weight;
    }

    [UnityEngine.ContextMenu("Sort")]
    public void SortEvents()
    {
        foreach (var item in headerList)
        {
            Destroy(item);
        }

        headerList = new List<GameObject>();

        StartCoroutine(SortEventCor());
    }

    IEnumerator SortEventCor()
    {
        transformObjs = new List<GameObject>();

        yield return StartCoroutine(SortAlgSplit(eventListObjects));

        List<GameObject> gos = new List<GameObject>();
        gos.AddRange(transformObjs);

        bool ongoingLabel = false;
        bool upcommingLabel = false;
        bool pastLabel = false;

        foreach (var item in gos)
        {
            item.transform.SetSiblingIndex(transformObjs.IndexOf(item));
        }

        foreach (var item in gos)
        {
            bool shouldInstantiate = false;
            string toWrite = "";

            EventListItemManager thisEvent = item.GetComponent<EventListItemManager>();

            Debug.Log(thisEvent.data.IsOngoing() + "," + thisEvent.data.IsPast() + ", " + thisEvent.data.IsUpcomming());

            if (thisEvent.data.IsOngoing() && !ongoingLabel)
            {
                shouldInstantiate = true;
                ongoingLabel = true;

                toWrite = ongoingName;
            }
            else if (thisEvent.data.IsPast() && !pastLabel)
            {
                shouldInstantiate = true;
                pastLabel = true;

                toWrite = pastName;
            }
            else if (thisEvent.data.IsUpcomming() && !upcommingLabel)
            {
                shouldInstantiate = true;
                upcommingLabel = true;

                toWrite = upcomignName;
            }

            if (shouldInstantiate)
            {
                GameObject label = Instantiate(header, eventListItemPrefabsTransform);
                headerList.Add(label);

                label.GetComponent<TMPro.TMP_Text>().text = toWrite;

                int index = item.transform.GetSiblingIndex();

                label.transform.SetSiblingIndex(index);
            }
        }

        gos = new List<GameObject>();

        Debug.Log("Done...");
    }

    public void UpdateDB()
    {
        UploadToDB("database", JsonUtility.ToJson(database));
    }

    public string UploadToStorage(string name, byte[] image, Action<float> progressAction, Action uploadedAction, Action failedAction)
    {
        StartCoroutine(UploadToStorageC(name, image, progressAction, uploadedAction, failedAction));

        return storageURL.Replace("[NAME]", name);
    }

    public void GetDataFromDB<T>(string authedURL, Action<T> onSuccess, Action onFailure)
    {
        StartCoroutine(GetFromDB(authedURL, onSuccess, onFailure));
    }

    public void ProcessRequestt<T>(UnityWebRequest req, Action<T> onSuccess = default, Action<string> onFailure = default)
    {
        StartCoroutine(ProccessWebRequest(req, false, onSuccess, onFailure));
    }

    IEnumerator GetInitial()
    {
        yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);

        if (!RedsFirebaseAuth.IsRefreshTokenValid() || databaseSecurityParameters.forceInValid)
        {
            Debug.Log("Token not valid!");
            LoginEmail loginEmail = new LoginEmail();

            redsFirebaseAuth.RefreshToken(loginEmail, ref firebaseLoginURLFormater.auth, firebaseLoginURLFormater);

            yield return new WaitUntil(() => !RedsFirebaseAuth.isRefreshingToken);

            yield return new WaitForEndOfFrame();

            Debug.Log("Refreshed!");
        }

        //Check if token is valid to allow auth
        if (redsFirebaseAuth.authStateManager.requireAttention)
        {
            if (redsFirebaseAuth.authStateManager.requireLogin)
            {
                Debug.Log("Login again!");
                firebaseLoginURLFormater.auth.token = "";
                redsFirebaseAuth.authStateManager.SetState(true, false, loginInFaliedStateName);

                yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);
                //yield return new WaitUntil(() => firebaseLoginURLFormater.auth.token.Length > 0);
            }
            else
            {
                Debug.Log("Cannot log you in!");
                yield break;
            }
        }

        string urlToReq = redsFirebaseAuth.AuthenticateURL(databaseURL.Replace("[PATH]", "database"), databaseSecurityParameters.requireAuthenticationForDB);

        #region Fetch Etag
        //Fetch etag at this location, print = silent so we only download the etag
        UnityWebRequest etagRequest = UnityWebRequest.Get(urlToReq + "&print=silent");
        etagRequest.SetRequestHeader("X-Firebase-ETag", "true");

        yield return etagRequest.SendWebRequest();

        string currentEtag = etagRequest.GetResponseHeader("ETag");
        #endregion

        string retreivedJSON = "";
        string error = "";
        bool isSuccess = false;

        if (RedsEtagManager.instance.CompareEtag(urlToReq, currentEtag))
        {
            retreivedJSON = RedsEtagManager.instance.GetContentJSON(urlToReq);
            error = "";
            isSuccess = retreivedJSON.Length > 0 && retreivedJSON != "null";

            Debug.Log("Fetching from local cache. Success " + isSuccess);
            Debug.Log("Cached json " + retreivedJSON);
        }
        else
        {
            Debug.Log("Fetching from online db");

            UnityWebRequest req = UnityWebRequest.Get(urlToReq);

            yield return req.SendWebRequest();

            retreivedJSON = req.downloadHandler.text;
            error = req.error;
            isSuccess = req.result == UnityWebRequest.Result.Success;

            RedsEtagManager.instance.AddNewEtag(urlToReq, currentEtag, retreivedJSON);
        }

        if (isSuccess)
        {
            try
            {
                database = JsonUtility.FromJson<Database>(retreivedJSON);
            }
            catch (Exception ex)
            {
                database = new Database();
                RedsVectorStatesManager.ShowVector(noEventStateName);
            }

            if (database.events == default)
            {
                database.events = new EventManager[0];
                RedsVectorStatesManager.ShowVector(noEventStateName);
            }

            yield return new WaitForEndOfFrame();

            Dictionary<EventManager, Texture2D> eventsDownloaded = new Dictionary<EventManager, Texture2D>();

            foreach (var item in database.events)
            {
                //Get texture
                Texture2D tex = new Texture2D(1, 1);

                if (RedsEtagManager.instance.CompareEtagImage(item.compressedImage))
                {
                    Debug.Log("Fetched image from local storage");
                    tex.LoadImage(RedsEtagManager.instance.GetContentBytes(item.compressedImage), false);
                }
                else
                {
                    Debug.Log("Downloaded image!");
                    UnityWebRequest imageReq = UnityWebRequestTexture.GetTexture(item.compressedImage);

                    yield return imageReq.SendWebRequest();

                    tex.LoadImage(imageReq.downloadHandler.data, false);

                    RedsEtagManager.instance.CacheImage(item.compressedImage, imageReq.downloadHandler.data);
                }

                eventsDownloaded.Add(item, tex);
            }

            yield return new WaitForEndOfFrame();

            foreach (var item in eventsDownloaded)
            {
                AddVisualEvent(item.Key, item.Value);
            }

            eventsDownloaded = new Dictionary<EventManager, Texture2D>();

            RedsVectorStatesManager.HideVector(loginInStateName);

            onDBFetched?.Invoke();
        }
        else
        {
            Debug.Log("Error");
            Debug.Log(error);
        }

        yield return new WaitForEndOfFrame();
        SortEvents();
    }

    public void GetImage(string imagePath, Action<Texture2D> onFetched)
    {
        StartCoroutine(imagePath, onDBFetched);
    }

    IEnumerator GetImageFromDB(string imagePath, Action<Texture2D> onFetched)
    {
        //Get texture
        Texture2D tex = new Texture2D(1, 1);

        if (RedsEtagManager.instance.CompareEtagImage(imagePath))
        {
            Debug.Log("Fetched image from local storage");
            tex.LoadImage(RedsEtagManager.instance.GetContentBytes(imagePath), false);
        }
        else
        {
            Debug.Log("Downloaded image!");
            UnityWebRequest imageReq = UnityWebRequestTexture.GetTexture(imagePath);

            yield return imageReq.SendWebRequest();

            tex.LoadImage(imageReq.downloadHandler.data, false);

            RedsEtagManager.instance.CacheImage(imagePath, imageReq.downloadHandler.data);
        }

        onFetched(tex);
    }

    /// <summary>
    /// URL has to be already authed
    /// </summary>
    /// <param name="authedURL"></param>
    /// <returns></returns>
    IEnumerator GetFromDB<T>(string authedURL, Action<T> onSuccess = default, Action onFailure = default)
    {
        yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);

        if (!RedsFirebaseAuth.IsRefreshTokenValid() || databaseSecurityParameters.forceInValid)
        {
            Debug.Log("Token not valid!");
            LoginEmail loginEmail = new LoginEmail();

            redsFirebaseAuth.RefreshToken(loginEmail, ref firebaseLoginURLFormater.auth, firebaseLoginURLFormater);

            yield return new WaitUntil(() => !RedsFirebaseAuth.isRefreshingToken);

            yield return new WaitForEndOfFrame();

            Debug.Log("Refreshed!");
        }

        //Check if token is valid to allow auth
        if (redsFirebaseAuth.authStateManager.requireAttention)
        {
            if (redsFirebaseAuth.authStateManager.requireLogin)
            {
                Debug.Log("Login again!");
                firebaseLoginURLFormater.auth.token = "";
                redsFirebaseAuth.authStateManager.SetState(true, false, loginInFaliedStateName);

                yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);
                //yield return new WaitUntil(() => firebaseLoginURLFormater.auth.token.Length > 0);
            }
            else
            {
                Debug.Log("Cannot log you in!");
                yield break;
            }
        }

        #region Fetch Etag
        //Fetch etag at this location, print = silent so we only download the etag
        UnityWebRequest etagRequest = UnityWebRequest.Get(authedURL + "&print=silent");
        etagRequest.SetRequestHeader("X-Firebase-ETag", "true");

        yield return etagRequest.SendWebRequest();

        string currentEtag = etagRequest.GetResponseHeader("ETag");
        #endregion

        string retreivedJSON = "";
        string error = "";
        bool isSuccess = false;

        if (RedsEtagManager.instance.CompareEtag(authedURL, currentEtag))
        {
            retreivedJSON = RedsEtagManager.instance.GetContentJSON(authedURL);
            error = "";
            isSuccess = retreivedJSON.Length > 0 && retreivedJSON != "null";

            Debug.Log("Fetching from local cache. Success " + isSuccess);
            Debug.Log("Cached json " + retreivedJSON);
        }
        else
        {
            Debug.Log("Fetching from online db");

            UnityWebRequest req = UnityWebRequest.Get(authedURL);

            yield return req.SendWebRequest();

            retreivedJSON = req.downloadHandler.text;
            error = req.error;
            isSuccess = req.result == UnityWebRequest.Result.Success;

            RedsEtagManager.instance.AddNewEtag(authedURL, currentEtag, retreivedJSON);
        }

        if (isSuccess)
        {
            string json = retreivedJSON;

            if (json != "null" && json.Length > 0)
            {
                try
                {
                    if (onSuccess != default)
                    {
                        onSuccess(JsonUtility.FromJson<T>(json));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Error " + ex.Message);
                }
            }
            else
            {
                if (onFailure != default)
                {
                    onFailure();
                }
            }
        }
        else
        {
            Debug.Log("Error");
            Debug.Log(error);

            if (onFailure != default)
            {
                onFailure();
            }
        }

        yield return new WaitForEndOfFrame();
        SortEvents();
    }

    IEnumerator UploadToStorageC(string name, byte[] image, Action<float> progressAction, Action uploadedAction, Action failedAction)
    {
        UploadHandlerRaw uH = new UploadHandlerRaw(image);
        uH.contentType = "image/png";

        UnityWebRequest req = new UnityWebRequest(redsFirebaseAuth.AuthenticateURL(storageURL.Replace("[NAME]", name), databaseSecurityParameters.requireAuthenticationForStorage), "POST");

        req.uploadHandler = uH;

        req.SendWebRequest();

        RedsEtagManager.instance.CacheImage(req.url, image);

        while (req.uploadProgress < 1)
        {
            float curr = req.uploadProgress;

            if (progressAction != default)
            {
                progressAction(curr);
            }

            yield return new WaitUntil(() => req.uploadProgress != curr);
        }

        yield return new WaitUntil(() => req.result != UnityWebRequest.Result.InProgress);

        Debug.Log("Done!" + req.result);

        if (req.result == UnityWebRequest.Result.Success)
        {
            if (uploadedAction != default)
            {
                uploadedAction();
            }
        }
        else
        {
            Debug.Log("Error " + req.error);
            if (failedAction != default)
            {
                failedAction();
            }
        }
    }

    IEnumerator ProccessWebRequest<T>(UnityWebRequest req, bool stroeToCache, Action<T> onSuccess = default, Action<string> onFailure = default)
    {
        yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);

        if (!RedsFirebaseAuth.IsRefreshTokenValid() || databaseSecurityParameters.forceInValid)
        {
            Debug.Log("Token not valid!");
            LoginEmail loginEmail = new LoginEmail();

            redsFirebaseAuth.RefreshToken(loginEmail, ref firebaseLoginURLFormater.auth, firebaseLoginURLFormater);

            yield return new WaitUntil(() => !RedsFirebaseAuth.isRefreshingToken);

            yield return new WaitForEndOfFrame();

            Debug.Log("Refreshed!");
        }

        //Check if token is valid to allow auth
        if (redsFirebaseAuth.authStateManager.requireAttention)
        {
            if (redsFirebaseAuth.authStateManager.requireLogin)
            {
                Debug.Log("Login again!");
                firebaseLoginURLFormater.auth.token = "";
                redsFirebaseAuth.authStateManager.SetState(true, false, loginInFaliedStateName);

                yield return new WaitUntil(() => redsFirebaseAuth.authStateManager.isAuthed);
                //yield return new WaitUntil(() => firebaseLoginURLFormater.auth.token.Length > 0);
            }
            else
            {
                Debug.Log("Cannot log you in!");
                yield break;
            }
        }

        #region Fetch Etag
        //Fetch etag at this location, print = silent so we only download the etag
        UnityWebRequest etagRequest = UnityWebRequest.Get(req.url + "&print=silent");
        etagRequest.SetRequestHeader("X-Firebase-ETag", "true");

        string currentEtag = "";

        if (stroeToCache)
        {
            yield return etagRequest.SendWebRequest();

            currentEtag = etagRequest.GetResponseHeader("ETag");
        }
        #endregion

        string retreivedJSON = "";
        string error = "";
        bool isSuccess = false;

        if (RedsEtagManager.instance.CompareEtag(req.url, currentEtag) && stroeToCache)
        {
            retreivedJSON = RedsEtagManager.instance.GetContentJSON(req.url);
            error = "";
            isSuccess = retreivedJSON.Length > 0 && retreivedJSON != "null";

            Debug.Log("Fetching from local cache. Success " + isSuccess);
            Debug.Log("Cached json " + retreivedJSON);
        }
        else
        {
            Debug.Log("Fetching from online db");

            yield return req.SendWebRequest();

            retreivedJSON = req.downloadHandler.text;
            error = req.error;
            isSuccess = req.result == UnityWebRequest.Result.Success;

            if (stroeToCache)
            {
                RedsEtagManager.instance.AddNewEtag(req.url, currentEtag, retreivedJSON);
            }
        }

        if (isSuccess)
        {
            string json = retreivedJSON;

            if (json != "null" && json.Length > 0)
            {
                try
                {
                    if (onSuccess != default)
                    {
                        onSuccess(JsonUtility.FromJson<T>(json));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Error " + ex.Message);
                }
            }
            else
            {
                if (onFailure != default)
                {
                    onFailure("JSON null");
                }
            }
        }
        else
        {
            Debug.Log("Error" + error);

            if (onFailure != default)
            {
                onFailure(retreivedJSON);
            }
        }

        yield return new WaitForEndOfFrame();
    }

    public static void SortStatic(List<GameObject> transformS)
    {
        networkManager.Sort(transformS);
    }

    public void Sort(List<GameObject> transformS)
    {
        transformObjs = new List<GameObject>();

        StartCoroutine(SortAlg(transformS));
    }

    public IEnumerator SortAlg(List<GameObject> transformObj)
    {
        yield return StartCoroutine(SortAlgSplit(transformObj));

        List<GameObject> gos = new List<GameObject>();
        gos.AddRange(transformObjs);

        foreach (var item in gos)
        {
            item.transform.SetSiblingIndex(transformObjs.IndexOf(item));
        }

        gos = new List<GameObject>();

        Debug.Log("Done...");
    }

    public IEnumerator SortAlgSplit(List<GameObject> transformObj)
    {
        transformObjs.AddRange(transformObj);

        bool sorted = false;
        int div = 2;

        while (!sorted)
        {
            sorted = true;

            List<List<Transform>> seps = new List<List<Transform>>();
            int half = (transformObjs.Count / div);

            Vector2Int indexSection = new Vector2Int(0, half);

            for (int q1 = 0; q1 < div; q1++)
            {
                seps.Add(new List<Transform>());

                for (int q2 = indexSection.x; q2 <= indexSection.y; q2++)
                {
                    if (q2 >= transformObjs.Count)
                    {
                        break;
                    }

                    seps[q1].Add(transformObjs[q2].transform);
                }

                indexSection.x = indexSection.y + 1;
                indexSection.y += half;
            }

            transformObjs = new List<GameObject>();

            for (int q = 0; q < seps.Count; q++)
            {
                List<Transform> trans = new List<Transform>();
                List<float> values = new List<float>();

                for (int i = 0; i < seps[q].Count; i++)
                {
                    trans.Add(seps[q][i]);
                    values.Add(GetValue(seps[q][i]));
                }

                while (trans.Count > 0)
                {
                    float max = -1;
                    int index = -1;

                    int i = 0;
                    foreach (var item in values)
                    {
                        if (max <= item)
                        {
                            sorted = false;

                            index = i;

                            max = item;
                        }
                        i++;
                    }

                    if (index != -1)
                    {
                        sorted = false;

                        SortTrans(trans[index], trans[trans.Count - 1]);

                        trans.RemoveAt(index);
                        values.RemoveAt(index);
                    }
                }
            }

            div--;

            if (div == 0)
            {
                sorted = true;
                //div = 2;
            }

            yield return new WaitForSeconds(0.0001f);
            transformObjs.Reverse();
        }
    }

    public void SortTrans(Transform trans1, Transform trans2)
    {
        //transformsList.Remove(trans1.gameObject);

        //int ind2 = trans2.GetSiblingIndex();

        //trans1.SetSiblingIndex(ind2);

        transformObjs.Add(trans1.gameObject);
    }

    public float GetValue(Transform transformToCheck)
    {
        return float.Parse(transformToCheck.gameObject.name);
    }
}

[System.Serializable]
public class Database
{
#nullable enable
    public EventDefaults? eventDefaults;
#nullable disable

    public EventManager[] events;
    public User[] users;

    Dictionary<string, int> userIndexing;

    /// <summary>
    /// Event ID to index
    /// </summary>
    Dictionary<string, int> eventIndexing;

    Dictionary<int, List<int>> userToEventIndexing;

    public User GetUser(string id)
    {
        CheckIndexing();

        if (userIndexing.ContainsKey(id))
        {
            return users[userIndexing[id]];
        }
        else
        {
            return default;
        }
    }

    public User GetUserFromEmail(string email)
    {
        CheckIndexing();

        foreach (var item in users)
        {
            if (item.email == email)
            {
                return item;
            }
        }

        return default;
    }

    public void CheckIndexing()
    {
        CheckArrays();

        if (userIndexing.Count != users.Length)
        {
            userIndexing = new Dictionary<string, int>();

            int i = 0;
            foreach (var user in users)
            {
                if (!userIndexing.ContainsKey(user.userID))
                {
                    userIndexing.Add(user.userID, i);
                }

                i++;
            }

            userToEventIndexing = new Dictionary<int, List<int>>();
        }

        if (eventIndexing.Count != events.Length)
        {
            eventIndexing = new Dictionary<string, int>();

            int i = 0;
            foreach (var eventM in events)
            {
                if (!eventIndexing.ContainsKey(eventM.eventID))
                {
                    eventIndexing.Add(eventM.eventID, i);
                }

                i++;
            }

            userToEventIndexing = new Dictionary<int, List<int>>();
        }

        if (userToEventIndexing.Count != userIndexing.Count)
        {
            userToEventIndexing = new Dictionary<int, List<int>>();

            List<User> tUsers = new List<User>(users);
            List<EventManager> tEvents = new List<EventManager>(events);

            foreach (var user in users)
            {
                List<int> allEvnts = new List<int>();

                foreach (var ev in events)
                {
                    List<User> allUsers = new List<User>();

                    try
                    {
                        foreach (var item in ev.attendees)
                        {
                            allUsers.Add(users[userIndexing[item]]);
                        }

                        if (allUsers.Contains(user))
                        {
                            allEvnts.Add(tEvents.IndexOf(ev));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex.Message);
                    }
                }

                userToEventIndexing.Add(tUsers.IndexOf(user), allEvnts);
            }
        }
    }

    public void CheckArrays()
    {
        if (events == default)
        {
            events = new EventManager[0];
        }

        if (users == default)
        {
            users = new User[0];
        }

        if (userIndexing == default)
        {
            userIndexing = new Dictionary<string, int>();
        }

        if (eventIndexing == default)
        {
            eventIndexing = new Dictionary<string, int>();
        }

        if (userToEventIndexing == default)
        {
            userToEventIndexing = new Dictionary<int, List<int>>();
        }
    }

    public void AddEvent(EventManager eventM)
    {
        CheckArrays();

        List<EventManager> mans = new List<EventManager>(events);
        mans.Add(eventM);

        events = mans.ToArray();
        mans = new List<EventManager>();
    }

    public int GetEventIndex(EventManager event_)
    {
        return GetEventIndex(event_.eventID);
    }

    public int GetEventIndex(string eventID)
    {
        CheckIndexing();

        if (eventIndexing.ContainsKey(eventID))
        {
            return eventIndexing[eventID];
        }

        return -1;
    }

    public EventManager GetEvent(string eventID)
    {
        CheckIndexing();

        int index = GetEventIndex(eventID);

        if (index > -1)
        {
            return events[index];
        }

        return default;
    }

    public void AddUser(User user)
    {
        CheckArrays();

        List<User> users = new List<User>();
        users.AddRange(this.users);

        if (!users.Contains(user))
        {
            users.Add(user);

            this.users = users.ToArray();

            CheckIndexing();
        }
    }

    public bool UserExists(string userID)
    {
        return userIndexing.ContainsKey(userID);
    }

    public bool UserExistsEmail(string email)
    {
        foreach (var item in users)
        {
            if (item.email == email)
            {
                return true;
            }
        }

        return false;
    }

    public List<EventManager> GetAllEventsForUser(User user)
    {
        CheckIndexing();

        List<EventManager> ls = new List<EventManager>();

        List<User> tUsers = new List<User>(users);
        int index = tUsers.IndexOf(user);

        foreach (var item in userToEventIndexing[index])
        {
            ls.Add(events[item]);
        }

        return ls;
    }

    public List<EventManager> GetAllEventsForUser(string userID)
    {
        CheckIndexing();

        List<EventManager> ls = new List<EventManager>();

        foreach (var evn in events)
        {
            if (evn.IsUserInEvent(userID))
            {
                ls.Add(evn);
            }
        }

        return ls;
    }
}

[Serializable]
public class EventManager
{
    public string eventID;
    public string eventName;

    public string eventTitle;
    public string eventDesc;

    public string startDate;
    public string endDate;

    public string timezone;

    public string[] speakers;

    public string[] attendees;
    public Attendence[] attendences;
    public float[] points;

    public string createdDate;

    public Ticketing ticketing;

    public string link;
    public string physicalAddress;

    public EventImageTemplate eventImageTemplate;

    /// <summary>
    /// Gives the url
    /// </summary>
    public string compressedImage;

    public Session[] sessions;

    public string[] logoURLs;

#nullable enable
    public ChatDatabase? chatDatabase;
    public SerializableHCT hctTheme;
#nullable disable

    List<User> attendeesUser;

    Dictionary<string, int> userIndex;

    string uncompressedImg;

    public Connection[] connections;

    public CertificateIssue[] certificates;

    public EmailTemplate emailTemplate;

    public StorableWebsitePacket websitePacket;

    /// <summary>
    /// Key is user id, value is for every connections they were mentioned in
    /// </summary>
    public Dictionary<string, List<int>> connectionsIndexing;

    //public int maxOfflineTickets;
    //public int maxOnlineTickets;

    public EventManager()
    {
        eventID = "";
        eventName = "";
        startDate = "";
        endDate = "";

        eventTitle = "";
        eventDesc = "";

        attendees = new string[0];
        attendences = new Attendence[0];
        speakers = new string[0];
        compressedImage = "";

        attendeesUser = new List<User>();
        userIndex = new Dictionary<string, int>();

        uncompressedImg = "";

        link = "";
        physicalAddress = "";

        hctTheme = new SerializableHCT(0, 0, 0, false);

        createdDate = DateTime.Today.ToString("MM dd yyyy");

        certificates = new CertificateIssue[0];
        eventImageTemplate = new EventImageTemplate("", "", new EventImageTemplate.EventImageDBProperties[0], new EventImageTemplate.EventImageDBProperties[0]);

        chatDatabase = new ChatDatabase();
    }

    public bool IsOngoing()
    {
        return DateTime.Parse(startDate) <= DateTime.Today && DateTime.Parse(endDate) >= DateTime.Today;
    }

    public bool IsPast()
    {
        return DateTime.Parse(startDate) <= DateTime.Today && DateTime.Parse(endDate) < DateTime.Today;
    }

    public bool IsUpcomming()
    {
        return !IsOngoing() && !IsPast();
    }

    public void AddPointsToUser(string id, float amount)
    {
        CheckIndexing();

        points[userIndex[id]] += amount;

        NetworkManager.networkManager.UpdateEventDB();
    }

    public EventManager(string eventID, string eventName, string eventTitle, string eventDesc, string timezone, string startDate, string endDate, List<string> speakers, List<string> attendees, string umcompressedImage, int maxOnlineTickets, int maxPhysicalTcikets, Session[] sessions, string[] logoURLs, string link, string physicalAddress, SerializableHCT serializableHCT, EventImageTemplate eventImageTemplate, int maxHybridTickets = 0)
    {
        this.eventID = eventID;
        this.eventName = eventName;
        this.startDate = startDate;
        this.endDate = endDate;

        this.eventDesc = eventDesc;
        this.eventTitle = eventTitle;

        this.timezone = timezone;

        this.attendees = new string[0];
        this.attendences = new Attendence[0];

        this.speakers = speakers.ToArray();

        this.points = new float[0];

        createdDate = DateTime.Today.ToString("MM dd yyyy");

        this.compressedImage = umcompressedImage;//RedsCompression.Compress(umcompressedImage);

        this.link = link;
        this.physicalAddress = physicalAddress;

        this.hctTheme = serializableHCT;

        certificates = new CertificateIssue[0];

        foreach (var item in attendees)
        {
            AddUser(item);
        }

        chatDatabase = new ChatDatabase();

        ticketing = new Ticketing(maxPhysicalTcikets, maxOnlineTickets, maxHybridTickets);

        this.sessions = sessions;
        this.logoURLs = logoURLs;

        this.eventImageTemplate = eventImageTemplate;
    }

    public bool IsUserInEvent(string id)
    {
        List<string> ids = new List<string>(attendees);
        bool isInList = ids.Contains(id);

        ids = default;

        return isInList;
    }

    public List<User> GetAttendees()
    {
        CheckIndexing();

        return attendeesUser;
    }

    public User GetUser(string id)
    {
        CheckIndexing();

        return attendeesUser[userIndex[id]];
    }

    public int GetPresenceForUser(string id)
    {
        CheckIndexing();

        if (IsUpcomming())
        {
            Debug.Log("Upcomming!");
            return 0;
        }

        Debug.Log($"Marked {ElapsedDaysNow()}");
        return attendences[userIndex[id]].GetPresence(ElapsedDaysNow()) ? 1 : 0;
    }

    public int GetTotalPresence(string id)
    {
        CheckIndexing();
        int count = 0;

        if (IsUpcomming())
        {
            return 0;
        }

        int day = 0;
        foreach (var item in attendences[userIndex[id]].attendence)
        {
            if (sessions[day].ticketing.IsParticipantIn(id))
            {
                count += item == 1 ? 1 : 0;
            }

            day++;
        }

        return count;
    }

    public int GetMaxPresence(string id)
    {
        CheckIndexing();
        int count = 0;

        int day = 0;
        foreach (var item in attendences[userIndex[id]].attendence)
        {
            if (sessions[day].ticketing.IsParticipantIn(id))
            {
                count++;
            }

            day++;
        }

        return count;
    }

    public float GetPercPresence(string id)
    {
        CheckIndexing();
        float count = 0;

        if (IsUpcomming())
        {
            return 0;
        }

        int day = 0;
        float totalReg = 0;
        foreach (var item in attendences[userIndex[id]].attendence)
        {
            if (sessions[day].ticketing.IsParticipantIn(id))
            {
                count += item == 1 ? 1 : 0;
                totalReg++;
            }

            day++;
        }

        totalReg = totalReg == 0 ? 1 : totalReg;

        return (count / totalReg) * 100f;
    }

    public string GetUncompressedString()
    {
        return compressedImage;

        if (uncompressedImg.Length <= 0)
        {
            uncompressedImg = RedsCompression.DeCompress(compressedImage);
        }

        return uncompressedImg;
    }

    public void CheckIndexing()
    {
        CheckArrays();

        if (attendeesUser.Count != attendees.Length)
        {
            attendeesUser = new List<User>();

            foreach (var item in attendees)
            {
                attendeesUser.Add(NetworkManager.networkManager.database.GetUser(item));
            }
        }

        if (userIndex.Count != attendees.Length)
        {
            userIndex = new Dictionary<string, int>();

            int i = 0;
            foreach (var item in attendeesUser)
            {
                if (!userIndex.ContainsKey(item.userID))
                {
                    userIndex.Add(item.userID, i);
                }

                i++;
            }
        }

        int connectionsCount = connections.Length;
        int indexingCount = connectionsIndexing.Keys.Count - 1;

        if (indexingCount != connectionsCount)
        {
            connectionsIndexing = new Dictionary<string, List<int>>();
            int index = 0;

            foreach (var connection in connections)
            {
                User initiator = connection.GetInitiatorUser(NetworkManager.networkManager.database);
                User other = connection.GetOtherUser(NetworkManager.networkManager.database);

                if (!connectionsIndexing.ContainsKey(initiator.userID))
                {
                    connectionsIndexing.Add(initiator.userID, new List<int>());
                }

                connectionsIndexing[initiator.userID].Add(index);

                if (!connectionsIndexing.ContainsKey(other.userID))
                {
                    connectionsIndexing.Add(other.userID, new List<int>());
                }

                connectionsIndexing[other.userID].Add(index);

                index++;
            }
        }
    }

    public void CheckArrays()
    {
        if (attendeesUser == default)
        {
            attendeesUser = new List<User>();
        }

        if (attendees == default)
        {
            attendees = new string[0];
        }

        if (userIndex == default)
        {
            userIndex = new Dictionary<string, int>();
        }

        if (attendences == default)
        {
            attendences = new Attendence[0];
        }

        if (chatDatabase == default)
        {
            chatDatabase = new ChatDatabase();
        }

        if (points == default || points.Length < attendees.Length)
        {
            points = new float[attendees.Length];
        }

        if (speakers == default)
        {
            speakers = new string[0];
        }

        if (sessions == default)
        {
            sessions = new Session[0];
        }

        if (logoURLs == default)
        {
            logoURLs = new string[0];
        }

        if (connections == default)
        {
            connections = new Connection[0];
        }

        if (connectionsIndexing == default)
        {
            connectionsIndexing = new Dictionary<string, List<int>>();
        }
    }

    public void AddMessage(string message)
    {
        CheckArrays();

        if (!RedsFirebaseAuth.instance.authStateManager.isAuthed)
        {
            Debug.LogError("Not Authed!");
            return;
        }

        chatDatabase.CreateNewChatMessage(RedsFirebaseAuth.instance.redsUserInfo.displayName, message);
    }

    public void AddUser(string id)
    {
        if (!UserExist(id))
        {
            List<string> tempUsers = new List<string>(attendees);
            tempUsers.Add(id);

            attendees = tempUsers.ToArray();

            List<Attendence> presences = new List<Attendence>(attendences);
            presences.Add(new Attendence(ElapsedDays()));

            attendences = presences.ToArray();

            tempUsers = new List<string>();
            presences = new List<Attendence>();

            List<float> temp = new List<float>(points);
            temp.Add(0);

            points = temp.ToArray();

            temp = default;

            List<CertificateIssue> certTemp = new List<CertificateIssue>();
            certTemp.Add(new CertificateIssue());

            certificates = certTemp.ToArray();

            certTemp = new List<CertificateIssue>();

            CheckIndexing();
        }
    }

    public void MarkUserPresent(string id, bool isPresent)
    {
        if (UserExist(id))
        {
            DateTime start = DateTime.Parse(startDate);
            DateTime end = DateTime.Parse(endDate);

            DateTime now = DateTime.Now;

            if (now <= end && now >= start)
            {
                TimeSpan elapsed = now - start;
                int elapsedDays = (int)elapsed.TotalDays;

                int index = userIndex[id];

                attendences[index].SetPresent(elapsedDays, isPresent ? 1 : 0);
            }
        }
    }

    public bool UserExist(string id)
    {
        CheckIndexing();

        return userIndex.ContainsKey(id);
    }

    public int ElapsedDays()
    {
        DateTime start = DateTime.Parse(startDate);
        DateTime end = DateTime.Parse(endDate);

        TimeSpan elapsed = end - start;
        int elapsedDays = (int)elapsed.TotalDays;

        return elapsedDays;
    }

    public int ElapsedDaysNow()
    {
        if (IsUpcomming())
        {
            return 0;
        }

        DateTime start = DateTime.Parse(startDate);
        DateTime end = DateTime.Now;

        TimeSpan elapsed = end - start;
        int elapsedDays = (int)elapsed.TotalDays;

        return elapsedDays;
    }

    [Serializable]
    public class Attendence
    {
        public int maxDays;
        public int[] attendence;

        public Attendence(int maxDays)
        {
            this.maxDays = maxDays;

            attendence = new int[maxDays];

            for (int i = 0; i < attendence.Length; i++)
            {
                attendence[i] = -1;
            }
        }

        public void SetPresent(int index, int present)
        {
            if (attendence.Length > index)
            {
                attendence[index] = present;
            }
        }

        public bool GetPresence(int day)
        {
            if (attendence.Length > day)
            {
                return attendence[day] == 1;
            }

            return false;
        }
    }

    [Serializable]
    public class Ticketing
    {
        public string[] physicalTickets;
        public string[] virtualTickets;
        public string[] hybridTickets;

        public int maxPhysicalTickets;
        public int maxVirtualTickets;
        public int maxHybridTickets;

        [SerializeField] List<string> physicalTicketsTemp;
        [SerializeField] List<string> virtualTicketsTemp;
        [SerializeField] List<string> hybridTicketsTemp;

        public Ticketing(int physicalTicket, int virtualTicket, int hybridTicket)
        {
            maxPhysicalTickets = physicalTicket;
            maxVirtualTickets = virtualTicket;
            maxHybridTickets = hybridTicket;

            physicalTickets = new string[0];
            virtualTickets = new string[0];
            hybridTickets = new string[0];

            CheckArrays();
        }

        public Ticketing()
        {
            physicalTickets = new string[0];
            virtualTickets = new string[0];
            hybridTickets = new string[0];

            CheckArrays();
        }

        void CheckArrays()
        {
            if (physicalTickets == default)
            {
                physicalTickets = new string[0];
                physicalTicketsTemp = new List<string>();
            }

            if (virtualTickets == default)
            {
                virtualTickets = new string[0];
                virtualTicketsTemp = new List<string>();
            }

            if (hybridTickets == default)
            {
                hybridTickets = new string[0];
                hybridTicketsTemp = new List<string>();
            }

            if (physicalTicketsTemp == default || physicalTicketsTemp.Count != physicalTickets.Length)
            {
                physicalTicketsTemp = new List<string>(physicalTickets);
            }

            if (virtualTicketsTemp == default || virtualTicketsTemp.Count != virtualTickets.Length)
            {
                virtualTicketsTemp = new List<string>(virtualTickets);
            }

            if (hybridTicketsTemp == default || hybridTicketsTemp.Count != hybridTickets.Length)
            {
                hybridTicketsTemp = new List<string>(hybridTickets);
            }
        }

#nullable enable
        public bool AddUser(TicketType ticketType, User? user)
        {
            CheckArrays();

            if (!IsAvailable(ticketType) || user == default || user.userID.Length <= 0)
            {
                return false;
            }

            switch (ticketType)
            {
                case TicketType.Physical:
                    physicalTicketsTemp.Add(user.userID);
                    physicalTickets = physicalTicketsTemp.ToArray();
                    break;
                case TicketType.Virtual:
                    virtualTicketsTemp.Add(user.userID);
                    virtualTickets = virtualTicketsTemp.ToArray();
                    break;
                case TicketType.Hybrid:
                    hybridTicketsTemp.Add(user.userID);
                    hybridTickets = hybridTicketsTemp.ToArray();
                    break;
                default:
                    return false;
            }

            return true;
        }
#nullable disable

        public bool IsAvailable(TicketType ticketType)
        {
            return GetAvailableTicketCount(ticketType) > 0;
        }

        public int GetAvailableTicketCount(TicketType ticketType)
        {
            int maxTickets = ticketType == TicketType.Physical ? maxPhysicalTickets : ticketType == TicketType.Virtual ? maxVirtualTickets : maxHybridTickets;

            return GetSoldTicketCount(ticketType) - maxTickets;
        }

        public int GetSoldTicketCount(TicketType ticketType)
        {
            int amountSold = (ticketType == TicketType.Physical ? physicalTickets : ticketType == TicketType.Virtual ? virtualTickets : hybridTickets).Length;

            return amountSold;
        }

        public List<User> GetAllParticipants(TicketType ticketType)
        {
            CheckArrays();

            List<User> participants = new List<User>();

            foreach (var participant in (ticketType == TicketType.Physical ? physicalTicketsTemp : ticketType == TicketType.Virtual ? virtualTicketsTemp : hybridTicketsTemp))
            {
                if (!string.IsNullOrEmpty(participant) && !string.IsNullOrWhiteSpace(participant))
                {
                    participants.Add(NetworkManager.networkManager.database.GetUser(participant));
                }
            }

            return participants;
        }

        public bool IsParticipantIn(string userID)
        {
            CheckArrays();

            return physicalTicketsTemp.Contains(userID) || hybridTicketsTemp.Contains(userID) || virtualTicketsTemp.Contains(userID);
        }

        public enum TicketType
        {
            Physical,
            Virtual,
            Hybrid
        }
    }

    [System.Serializable]
    public class SerializableHCT
    {
        public float hue;
        public float chroma;
        public float tone;

        public bool isDarkMode;

        public SerializableHCT(RedsHCT hct, bool isDarkMode)
        {
            Initialize(hct.hue, hct.chroma, hct.tone);
            this.isDarkMode = isDarkMode;
        }

        public SerializableHCT(float hue, float chroma, float tone, bool isDarkMode)
        {
            Initialize(hue, chroma, tone);
            this.isDarkMode = isDarkMode;
        }

        public void Initialize(float hue, float chroma, float tone)
        {
            this.hue = hue;
            this.chroma = chroma;
            this.tone = tone;
        }

        public RedsHCT GetHCT()
        {
            return new RedsHCT(hue, chroma, tone);
        }
    }

    [Serializable]
    public class Session
    {
        public string sessionName;

        public string sessionDescription;
        public string sessionDate;

        public string sessionBannerURL;
        public int[] speakersIndex;

        public Ticketing ticketing;

        public Session(string sessionName, string sessionDescription, string sessionDate, string sessionBannerURL, int[] speakersIndex, Ticketing ticketing)
        {
            InitializeValues(sessionName, sessionDescription, sessionDate, sessionBannerURL, speakersIndex, ticketing);
        }

        public Session(SessionChipData sessionChipData, int[] speakersIndex, Ticketing ticketing)
        {
            InitializeValues(sessionChipData.sessionName, sessionChipData.sessionDesc, sessionChipData.sessionDate, sessionChipData.sessionImageURL, speakersIndex, ticketing);
        }

        void InitializeValues(string sessionName, string sessionDescription, string sessionDate, string sessionBannerURL, int[] speakersIndex, Ticketing ticketing)
        {
            this.sessionName = sessionName;
            this.sessionDescription = sessionDescription;

            this.sessionDate = sessionDate;
            this.sessionBannerURL = sessionBannerURL;
            this.speakersIndex = speakersIndex;

            this.ticketing = ticketing;
        }
    }

    [Serializable]
    public class EmailTemplate
    {
        public Email physicalEmailTemplate;
        public Email virtualEmailTemplate;

        [Serializable]
        public class Email
        {
            public string subject;
            public string body;
        }
    }
}

[Serializable]
public class CertificateIssue
{
    public bool issued;
    public bool isError;
    public bool reported;
}

[Serializable]
public class User
{
    public UserType userType;

    public string userID;
    public string name;

    public string email;

    public string[] connections;
    public string designation;
    public string bio;
    public string country;
    public string interest;
    public string industry;
    public string profileURL;
    public string organisationName;

    public string zoomEmail;
    public string zoomID;

    public Meeting[] pendingMeetings;
    public Meeting[] acceptedMeetings;
    public Meeting[] pastMeetings;

    public User()
    {
        userType = UserType.Attendees;
        userID = "";
    }

    public User(UserType userType, string userID, string userName, string email, string country, string interest, string industry, string profileURL, string bio, string designation, string orgName)
    {
        this.userID = userID;
        this.userType = userType;
        this.name = userName;

        this.email = email;
        this.country = country;
        this.industry = industry;

        this.interest = interest;
        this.profileURL = profileURL;
        this.bio = bio;

        this.designation = designation;
        this.organisationName = orgName;
    }

    public void GetQRCode(string eventID)
    {
        QRManager.qRManager.inp = new QRData(userID, eventID);
        QRManager.qRManager.Gen();
    }

    public bool IsEligibleForCert(EventManager eventManager)
    {
        int sessionCountUser = Mathf.CeilToInt(eventManager.GetPercPresence(userID));

        if (IsRegular())
        {
            return sessionCountUser >= EventDefaultsManager.eventDefaults.ecertificateRegularEligibilityPercentage;
        }
        else
        {
            return sessionCountUser >= EventDefaultsManager.eventDefaults.ecertificateEligibilityPercentage;
        }
    }

    public bool IsRegular()
    {
        int percentageAcceptance = EventDefaultsManager.eventDefaults.ecertificateRegularEligibilityPercentage;
        int sessionCount = 0;
        int sessionCountUser = 0;

        foreach (var item in NetworkManager.networkManager.database.events)
        {
            sessionCount += item.sessions.Length;

            if (item.UserExist(userID))
            {
                sessionCountUser += item.GetTotalPresence(userID);
            }
        }

        return Mathf.CeilToInt((sessionCountUser / sessionCount) * 100) >= percentageAcceptance;
    }

    public enum UserType
    {
        Admin,
        Logistics,
        Speakers,
        Sponsors,
        Attendees
    }
}

[Serializable]
public class Connection
{
    public ConnectionUsers connectionUsers;
    public bool isMeeting;

    public Connection(User initiator, User otherUser, bool isMeeting)
    {
        Initialize(new ConnectionUsers(initiator, otherUser));
        this.isMeeting = isMeeting;
    }

    public Connection(ConnectionUsers connectionUsers, bool isMeeting)
    {
        Initialize(connectionUsers);
        this.isMeeting = isMeeting;
    }

    void Initialize(ConnectionUsers connectionUsers)
    {
        this.connectionUsers = connectionUsers;
    }

    public User GetInitiatorUser(Database database)
    {
        return database.GetUser(connectionUsers.initatorUserID);
    }

    public User GetOtherUser(Database database)
    {
        return database.GetUser(connectionUsers.otherUserID);
    }

    [Serializable]
    public class ConnectionUsers
    {
        public string initatorUserID;
        public string otherUserID;

        public ConnectionUsers(User initiator, User otherUser)
        {
            this.initatorUserID = initiator.userID;
            this.otherUserID = otherUser.userID;
        }
    }
}

[Serializable]
public class Meeting
{
    public string eventID;
    public int connectionIndex;
    public bool accepted;

    public Meeting(string eventID, int connectionIndex)
    {
        this.eventID = eventID;
        this.connectionIndex = connectionIndex;
        accepted = false;
    }

    public void AcceptRequest(bool accept)
    {
#nullable enable
        Connection? connection = GetConnection(NetworkManager.networkManager.database);
        EventManager? event_ = GetEvent(NetworkManager.networkManager.database);
#nullable disable

        if (connection == default || event_ == default)
        {
            Debug.LogError("Connection or event is null");
            return;
        }

        User thisUser = connection.GetOtherUser(NetworkManager.networkManager.database);
        User initator = connection.GetInitiatorUser(NetworkManager.networkManager.database);

        thisUser.pendingMeetings = RemoveFromArray(thisUser.pendingMeetings, this);
        initator.pendingMeetings = RemoveFromArray(initator.pendingMeetings, this);

        if (accept)
        {
            thisUser.acceptedMeetings = AddToArray(thisUser.acceptedMeetings, this);
            initator.acceptedMeetings = AddToArray(initator.acceptedMeetings, this);
        }
        else
        {
            thisUser.pastMeetings = AddToArray(thisUser.pastMeetings, this);
            initator.pastMeetings = AddToArray(initator.pastMeetings, this);
        }

        NetworkManager.networkManager.UpdateDB();
    }

    public EventManager GetEvent(Database database)
    {
        return database.GetEvent(eventID);
    }

    public Connection GetConnection(Database database)
    {
        if (GetEvent(database).connections.Length > connectionIndex)
        {
            return GetEvent(database).connections[connectionIndex];
        }

        return default;
    }

    public T[] AddToArray<T>(T[] values, T value)
    {
        List<T> temp = values == default ? new List<T>() : new List<T>(values);
        temp.Add(value);

        return temp.ToArray();
    }

    public T[] RemoveFromArray<T>(T[] values, T value)
    {
        List<T> temp = values == default ? new List<T>() : new List<T>(values);
        temp.Remove(value);

        return temp.ToArray();
    }
}

public static class RedsCompression
{
    public static string Compress(string uncomrpessed)
    {
        string compressed = "";

        char lastChar = uncomrpessed[0];
        int count = 0;

        foreach (char item in uncomrpessed)
        {
            if (item == lastChar)
            {
                count++;
            }
            else
            {
                compressed += lastChar + count + "|";
                count = 1;
                lastChar = item;
            }
        }

        return compressed;
    }

    public static string DeCompress(string compressed)
    {
        string unCompressed = "";

        char lastChar = compressed[0];
        int count = 0;

        foreach (var item in compressed.Split("|", StringSplitOptions.RemoveEmptyEntries))
        {
            lastChar = item[0];
            count = int.Parse(item.Replace(lastChar + "", ""));

            for (int i = 0; i < count; i++)
            {
                unCompressed += lastChar;
            }
        }

        return unCompressed;
    }
}

[Serializable]
public class DatabaseSecurityParameters
{
    public bool requireAuthenticationForDB;
    public bool requireAuthenticationForStorage;

    public bool forceRenewToken;
    public bool forceInValid;
}
