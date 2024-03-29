﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using P3.Driver.Sonos.Upnp.Proxy;
using P3.Driver.Sonos.Upnp.Services.Factories;
using P3.Driver.Sonos.Upnp.Services.Models;

namespace P3.Driver.Sonos.Upnp.Services
{
    public class AvTransportService : IAvTransportService
    {
        private readonly ILogger _logger;
        private readonly IUpnpClient _upnpClient;

        private const string ControlUrl = "/MediaRenderer/AVTransport/Control";
        private const string ActionNamespace = "urn:schemas-upnp-org:service:AVTransport:1";

        private static XNamespace ActionXNamespace => ActionNamespace;

        public AvTransportService(string ipAddress, ILogger logger)
        {
            _logger = logger;
            var upnpUri = new SonosUri(ipAddress, ControlUrl);
            _upnpClient = new UpnpClient(upnpUri.ToUri(), ActionNamespace);
        }

        public async Task StopAsync()
        {
            var response = await _upnpClient.InvokeActionAsync("Stop");
            await LogResponse($"{nameof(StopAsync)}", response);
        }

        public async Task PauseAsync()
        {
            var response = await _upnpClient.InvokeActionAsync("Pause");
            await LogResponse($"{nameof(PauseAsync)}", response);
        }

        public async Task PlayAsync(int playSpeed = 1)
        {
            var response = await _upnpClient.InvokeActionAsync("Play", new List<UpnpArgument>
            {
                new UpnpArgument("Speed", playSpeed)
            });
;
            await LogResponse($"{nameof(PlayAsync)}", response);
        }

        private async Task LogResponse(string responseType, HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogTrace($"{responseType} returned {response.StatusCode} with body {body}");
        }

        public async Task NextTrackAsync()
        {
            var response = await _upnpClient.InvokeActionAsync("Next");
            await LogResponse($"{nameof(NextTrackAsync)}", response);
        }

        public async Task PreviousTrackAsync()
        {
            var response = await _upnpClient.InvokeActionAsync("Previous");
            await LogResponse($"{nameof(PreviousTrackAsync)}", response);
        }

        public async Task SeekAsync(SeekUnit seekUnit, string position)           // (REL_TIME) position = hh:mm:ss, (TRACK_NR) position = track number
        {
            var response = await _upnpClient.InvokeActionAsync("Seek", new List<UpnpArgument>
            {
                new UpnpArgument("Unit", seekUnit.Value),
                new UpnpArgument("Target", position)
            });
            await LogResponse($"{nameof(SeekAsync)}", response);
        }

        public async Task ClearQueueAsync()
        {
            var response = await _upnpClient.InvokeActionAsync("RemoveAllTracksFromQueue");
            await LogResponse($"{nameof(ClearQueueAsync)}", response);
        }

        public async Task RemoveTrackFromQueueAsync(QueueItemId queueItemId)
        {
            var response = await _upnpClient.InvokeActionAsync("RemoveTrackFromQueue", new List<UpnpArgument>
            {
                new UpnpArgument("ObjectID", queueItemId.ObjectId)
            });

            await LogResponse($"{nameof(RemoveTrackFromQueueAsync)}", response);
        }

        public async Task<AddUriToQueueResponse> AddTrackToQueueAsync(string enqueuedUri, int desiredFirstTrackNumberEnqueued = 0, bool enqueueAsNext = false) 
        {
            if(desiredFirstTrackNumberEnqueued < 0)
                throw new ArgumentException("DesiredFirstTrackNumberEnqueued must either be zero (place at end of queue) or one or more (position in queue).", nameof(desiredFirstTrackNumberEnqueued));

            // enqueueAsNext = Whether this URI should be played as the next track in shuffle mode. This only works if PlayMode = SHUFFLE
            
            var xml = await _upnpClient.InvokeFuncWithResultAsync("AddURIToQueue", new List<UpnpArgument>
            {
                new UpnpArgument("EnqueuedURI", enqueuedUri),
                new UpnpArgument("EnqueuedURIMetaData", string.Empty),
                new UpnpArgument("DesiredFirstTrackNumberEnqueued", desiredFirstTrackNumberEnqueued),
                new UpnpArgument("EnqueueAsNext", enqueueAsNext.ToInt())
            });

            return new AddUriToQueueResponseFactory().CreateFor(ActionXNamespace, xml);
        }

        public async Task AddSpotifyTrackToQueueAsync(string spotifyId)
        {
            var rand = new Random();
            var randNumber = rand.Next(10000000, 99999999);

            var uri = $"x-sonos-spotify:spotify%3atrack%3a{spotifyId}";

            var metadata = "<DIDL-Lite xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:r=\"urn:schemas-rinconnetworks-com:metadata-1-0/\" xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\">" +
                            $"<item id=\"{randNumber}spotify%3atrack%3a{spotifyId}\" restricted=\"true\">" +
                               "<dc:title></dc:title>" +
                               $"<upnp:class>{ItemClass.MusicTrack}</upnp:class>" +
                               "<desc id=\"cdudn\" nameSpace=\"urn:schemas-rinconnetworks-com:metadata-1-0/\">SA_RINCON2311_X_#Svc2311-0-Token</desc>" +
                            "</item>" +
                           "</DIDL-Lite>";

            metadata = HttpUtility.HtmlEncode(metadata);

            //await AddTrackToQueueAsync(uri, metadata);
            await AddTrackToQueueAsync(uri);
        }

        public async Task SetPlayModeAsync(PlayMode playMode)
        {
            var response = await _upnpClient.InvokeActionAsync("SetPlayMode", new List<UpnpArgument>
            {
                new UpnpArgument("NewPlayMode", playMode.Value)
            });
            await LogResponse($"{nameof(SetPlayModeAsync)}", response);
        }

        /// <summary>
        /// Get information about media
        /// </summary>
        public async Task<GetMediaInfoResponse> GetMediaInfoAsync()
        {
            var xml = await _upnpClient.InvokeFuncWithResultAsync("GetMediaInfo");
            
            return new GetMediaInfoResponseFactory().CreateFor(ActionXNamespace, xml);
        }

        /// <summary>
        /// Gets current track info
        /// </summary>
        public async Task<GetPositionInfoResponse> GetPositionInfoAsync()
        {
            var xml = await _upnpClient.InvokeFuncWithResultAsync("GetPositionInfo");

            var result = new GetPositionInfoResponseFactory().CreateFor(ActionXNamespace, xml);

            return result;
        }

        /// <summary>
        /// Get status about the player
        /// </summary>
        public async Task<GetTransportInfoResponse> GetTransportInfoAsync()
        {
            var xml = await _upnpClient.InvokeFuncWithResultAsync("GetTransportInfo");

            return new GetTransportInfoResponseFactory().CreateFor(ActionXNamespace, xml);
        }

        /// <summary>
        /// Get PlayMode about player
        /// </summary>
        public async Task<GetTransportSettingsResponse> GetTransportSettingsAsync()
        {
            var xml = await _upnpClient.InvokeFuncWithResultAsync("GetTransportSettings");

            return new GetTransportSettingsResponseFactory().CreateFor(ActionXNamespace, xml);
        }

        private string GetRadioMetadata(string uri, string serviceType)
        {
            var metadata = $"<DIDL-Lite xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" " +
                           "xmlns:r=\"urn:schemas-rinconnetworks-com:metadata-1-0/\" xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\">" +
                           $"<item id=\"F00092020s${uri}\" parentID=\"L\"  restricted=\"true\">" +
                           "<dc:title>tunein</dc:title>" +
                           $"<upnp:class>{ItemClass.Stream}</upnp:class>" +
                           $"<desc id=\"cdudn\" nameSpace=\"urn:schemas-rinconnetworks-com:metadata-1-0/\">SA_RINCON{serviceType}_</desc>" +
                           "</item>" +
                           "</DIDL-Lite>";

            return metadata;
        }

        public async Task SetMediaUrl(string url)
        {
            var uriMetadata = new UpnpArgument("CurrentURIMetaData", GetRadioMetadata("", "65031_"));
            var xml = await _upnpClient.InvokeFuncWithResultAsync("SetAVTransportURI",
                new List<UpnpArgument>()
                    {new UpnpArgument("InstanceID", 0), new UpnpArgument("CurrentURI", url), uriMetadata});
        }

        /// <summary>
        /// Plays a radio on the Sonos
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task SetTuneInRadio(int radioId)
        {
            var radioUrl = $"x-sonosapi-stream:s{radioId}?sid=254&amp;flags=32";

            var uriMetadata = new UpnpArgument("CurrentURIMetaData", GetRadioMetadata("", "65031_"));
                var xml = await _upnpClient.InvokeFuncWithResultAsync("SetAVTransportURI",
                new List<UpnpArgument>()
                    {new UpnpArgument("InstanceID", 0), new UpnpArgument("CurrentURI", radioUrl), uriMetadata});
        }
    }
}
