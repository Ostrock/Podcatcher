﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Media.Imaging;
using System.IO.IsolatedStorage;
using System.Data.Linq.Mapping;
using System.Data.Linq;
using Podcatcher.ViewModels;

namespace Podcatcher
{
    [Table(Name="PodcastSubscription")]
    public class PodcastSubscriptionModel : INotifyPropertyChanged
    {        
        /************************************* Public properties *******************************/
        #region properties
        private int m_podcastId;
        [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
        public int PodcastId 
        {
            get { return m_podcastId; }
            set { m_podcastId = value; }
        }

        private string m_PodcastName;
        [Column]
        public string PodcastName
        {
            get
            {
                return m_PodcastName;
            }
            
            set
            {
                if (value != m_PodcastName)
                {
                    m_PodcastName = value;
                    NotifyPropertyChanged("PodcastName");
                }
            }
        }

        private string m_PodcastDescription;
        [Column]
        public string PodcastDescription
        {
            get
            {
                return m_PodcastDescription;
            }

            set
            {
                if (value != m_PodcastDescription)
                {
                    m_PodcastDescription = value;
                    NotifyPropertyChanged("PodcastDescription");
                }
            }
        }

        private string m_PodcastLogoLocalLocation;
        [Column]
        public string PodcastLogoLocalLocation
        {
            get
            {
                return m_PodcastLogoLocalLocation;
            }

            set
            {
                m_PodcastLogoLocalLocation = value;
                NotifyPropertyChanged("PodcastLogoLocalLocation");
            }
        }

        private Uri m_PodcastLogoUrl = null;
        public Uri PodcastLogoUrl
        {
            get
            {
                return m_PodcastLogoUrl;
            }

            set
            {
                if (value != m_PodcastLogoUrl)
                {
                    m_PodcastLogoUrl = value;
                }
            }
        }

        public BitmapImage PodcastLogo
        {
            get
            {
                if (m_PodcastLogoLocalLocation == null)
                {
                    return null;
                }

                // This method can be called when we still don't have a local
                // image stored, so the file name can be empty.
                string isoFilename = m_PodcastLogoLocalLocation;

                // When we request for the podcast logo we will in fact fetch
                // the image from the local cache, create the BitmapImage object
                // and return that. 
                IsolatedStorageFile isoStore = IsolatedStorageFile.GetUserStoreForApplication();
                if (isoStore.FileExists(isoFilename) == false)
                {
                    return null;
                }

                BitmapImage image = new BitmapImage();
                using (var stream = isoStore.OpenFile(isoFilename, System.IO.FileMode.OpenOrCreate))
                {
                    image.SetSource(stream);
                }
                return image;
            }

            private set { }
        }

        [Column]
        public DateTime LastUpdateTimestamp
        {
            get;
            set;
        }

        [Column]
        public String PodcastRSSUrl
        {
            get;
            set;
        }

        [Column]
        public String PodcastShowLink
        {
            get;
            set;
        }

        private EntitySet<PodcastEpisodeModel> m_podcastEpisodes = new EntitySet<PodcastEpisodeModel>();
        [Association(Storage = "m_podcastEpisodes", ThisKey = "PodcastId", OtherKey = "PodcastId")]
        public EntitySet<PodcastEpisodeModel> Episodes
        {
            get { return m_podcastEpisodes; }
            set {
                NotifyPropertyChanging();
                m_podcastEpisodes.Assign(value);
                NotifyPropertyChanged("Episodes");
            }
        }

        public string CachedPodcastRSSFeed
        {
            get;
            set;
        }

        public PodcastEpisodesManager EpisodesManager
        {
            get
            {
                return m_podcastEpisodesManager;
            }
        }

        // Version column aids update performance.
        [Column(IsVersion = true)]
        private Binary version;

        #endregion

        /************************************* Public implementations *******************************/
        
        public PodcastSubscriptionModel()
        {
            m_podcastEpisodesManager = new PodcastEpisodesManager(this);

            m_localPodcastIconCache = IsolatedStorageFile.GetUserStoreForApplication();
            m_localPodcastIconCache.CreateDirectory(App.PODCAST_ICON_DIR);
        }

        public void cleanupForDeletion()
        {
            // TODO: Extract methods.
                        
            // Delete logo from local image cache.
            if (m_localPodcastIconCache.FileExists(m_PodcastLogoLocalLocation) == false) 
            {
                Debug.WriteLine("ERROR: Logo local cache file not found! Subscription: " + m_PodcastName 
                                + ", logo file: " + m_PodcastLogoLocalLocation);
                return;
            }

            Debug.WriteLine("Deleting local cache of logo: " + m_PodcastLogoLocalLocation);
            m_localPodcastIconCache.DeleteFile(m_PodcastLogoLocalLocation);
        }

        public void fetchChannelLogo()
        {
            Debug.WriteLine("Getting podcast icon: " + m_PodcastLogoUrl);

            // Fetch the remote podcast logo to store locally in the IsolatedStorage.
            WebClient wc = new WebClient();
            wc.OpenReadCompleted += new OpenReadCompletedEventHandler(wc_FetchPodcastLogoCompleted);
            wc.OpenReadAsync(m_PodcastLogoUrl);
        }

        /************************************* Private implementation *******************************/
        #region privateImplementations        
        private PodcastEpisodesManager  m_podcastEpisodesManager;
        private IsolatedStorageFile     m_localPodcastIconCache;

        private void wc_FetchPodcastLogoCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                Debug.WriteLine("ERROR: Cannot fetch channel logo.");
                return;
            }

            Debug.WriteLine("Storing podcast icon locally...");

            Stream logoInStream = null;
            try
            {
                logoInStream = (Stream)e.Result;
            }
            catch (WebException webEx)
            {
                if (webEx.Status != WebExceptionStatus.Success)
                {
                    Debug.WriteLine("ERROR: Web error occured. Cannot load image!");
                    return;
                }
            }

            // Store the downloaded podcast logo to isolated storage for local cache.
            MemoryStream logoMemory = new MemoryStream();
            logoInStream.CopyTo(logoMemory);
            using (var isoFileStream = new IsolatedStorageFileStream(m_PodcastLogoLocalLocation, 
                                                                     FileMode.OpenOrCreate, 
                                                                     m_localPodcastIconCache))
            {
                isoFileStream.Write(logoMemory.ToArray(), 0, (int)logoMemory.Length);
            }

            Debug.WriteLine("Stored local podcast icon as: " + PodcastLogoLocalLocation);
            
            // Local cache has been updated - notify the UI that the logo property has changed.
            // and the new logo can be fetched to the UI. 
            NotifyPropertyChanged("PodcastLogo");
        }

        #region propertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
		
		private void NotifyPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, null);
			}
		}

        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
        #endregion
    }
}