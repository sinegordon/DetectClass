using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO;


using Xamarin.Forms;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Acr.UserDialogs;

using Microsoft.ProjectOxford.Face;

namespace EmployeeDirectory
{
	public class EmployeesViewModel : BaseViewModel
	{
		string personGroupId;
		string data = "";

		private void ResponseCallback(IAsyncResult result)
		{
			try
			{
				var request = (HttpWebRequest)result.AsyncState;
				var response = request.EndGetResponse(result);
				using (var stream = response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					data = reader.ReadToEnd();
					Employees = new List<Employee>();
					string[] strmas = data.Split('\n');
					for (int i = 0; i < strmas.Length; i += 1)
					{
						string str = strmas[i].Split('/').Last().Split('.').First();
						string[] ng = str.Split('_');
						Employees.Add(new Employee { Name = ng[0], Class = ng[1], PhotoUrl = strmas[i] });
					}
				}
				RegisterEmployees();
			}
			catch (Exception ex)
			{
			}
		}

		public EmployeesViewModel()
		{
			Title = "Persons";
			var listUrl = "http://res.cloudinary.com/djxicvnef/raw/upload/v1474574352/list.txt";
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(listUrl));
			request.ContentType = "application/text";
			request.Method = "GET";
			request.BeginGetResponse(ResponseCallback, request);
			//string list = 
			/*
			 * Employees = new ObservableCollection<Employee>
			{
				new Employee { Name = "Папа", Class = "CEO", PhotoUrl = "http://res.cloudinary.com/djxicvnef/image/upload/v1473954627/Pop.jpg" },
				new Employee { Name = "Мама", Class = "CEO", PhotoUrl = "http://res.cloudinary.com/djxicvnef/image/upload/v1473961818/Mom.jpg" }

			};
			*/
		}

		List<Employee> employees;
		public List<Employee> Employees
		{
			get { return employees; }
			set { employees = value; OnPropertyChanged("Employees"); }
		}

		Command findSimilarFaceCommand;
		public Command FindSimilarFaceCommand
		{
			get { return findSimilarFaceCommand ?? (findSimilarFaceCommand = new Command(async () => await ExecuteFindSimilarFaceCommandAsync())); }
		}

		async Task ExecuteFindSimilarFaceCommandAsync()
		{
			if (IsBusy)
				return;

			IsBusy = true;

			try
			{
				MediaFile photo;

				await CrossMedia.Current.Initialize();

				// Take or select a photo using the Media Plugin for Xamarin and Windows
				if (CrossMedia.Current.IsCameraAvailable)
				{
					photo = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
					{
						Directory = "Employee Directory",
						Name = "photo.jpg"
					});
				}
				else {
					photo = await CrossMedia.Current.PickPhotoAsync();
				}
				using (var stream = photo.GetStream())
				{
					var faceServiceClient = new FaceServiceClient("27db8efe5d9348bd8069a91d1f7935f6");

					// Step 4a - Detect the faces in this photo.
					var faces = await faceServiceClient.DetectAsync(stream);
					var faceIds = faces.Select(face => face.FaceId).ToArray();

					// Step 4b - Identify the person in the photo, based on the face.
					var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
					// Step 4c - Fetch the person from the PersonId and display their name.
					var str = "";
					foreach (var result in results)
					{
						var cand = result.Candidates[0].PersonId;
						var person = await faceServiceClient.GetPersonAsync(personGroupId, cand);
						str += person.Name + "\n";
					}
					UserDialogs.Instance.ShowSuccess($"Person(s) identified as " + str);
				}
			}
			catch (Exception ex)
			{
				UserDialogs.Instance.ShowError(ex.Message);
			}
			finally
			{
				IsBusy = false;
			}
		}

		async Task RegisterEmployees()
		{
			var faceServiceClient = new FaceServiceClient("27db8efe5d9348bd8069a91d1f7935f6");

			// Step 1 - Create Person Group
			personGroupId = Guid.NewGuid().ToString();
			await faceServiceClient.CreatePersonGroupAsync(personGroupId, "University Employees");

			// Step 2 - Add persons (and faces) to person group.
			foreach (var employee in Employees)
			{
				// Step 2a - Create a new person, identified by their name.
				var p = await faceServiceClient.CreatePersonAsync(personGroupId, employee.Name);
				// Step 3a - Add a face for that person.
				await faceServiceClient.AddPersonFaceAsync(personGroupId, p.PersonId, employee.PhotoUrl);
			}

			// Step 3 - Train facial recognition model.
			await faceServiceClient.TrainPersonGroupAsync(personGroupId);
		}
	}
}