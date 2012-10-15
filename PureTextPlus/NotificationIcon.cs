﻿/*
    PureText+ - http://code.google.com/p/puretext-plus/
    
    Copyright (C) 2003 Steve P. Miller, http://www.stevemiller.net/puretext/
    Copyright (C) 2011 Melloware, http://www.melloware.com
    Copyright (C) 2012 Anderson Direct Marketing, http://www.andersondm.com
    
   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
    
    The idea of the Original PureText Code is Copyright (C) 2003 Steve P. Miller
    
    NO code was taken from the original project this was rewritten from scratch
    from just the idea of Puretext.
*/
using System;
using System.Diagnostics;
using System.Drawing;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;

namespace PureTextPlus
{
	/// <summary>
	/// Main class of the application which displays the notification icon and business logic.
	/// </summary>
	public sealed class NotificationIcon
	{
		private NotifyIcon notifyIcon;
		private ContextMenu notificationMenu;
		private static readonly HotkeyHook hotkey = new HotkeyHook();
		private static readonly HotkeyHook plainHotKey = new HotkeyHook();
		private static readonly HotkeyHook htmlHotKey = new HotkeyHook();
		
		#region Initialize icon and menu
		public NotificationIcon()
		{
			notifyIcon = new NotifyIcon();
			notificationMenu = new ContextMenu(InitializeMenu());
			
			notifyIcon.DoubleClick += IconDoubleClick;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotificationIcon));
			notifyIcon.Icon = (Icon)resources.GetObject("$this.Icon");
			notifyIcon.ContextMenu = notificationMenu;
			
			// register the event that is fired after the key press.
			hotkey.KeyPressed += new EventHandler<KeyPressedEventArgs>(Hotkey_KeyPressed);
			plainHotKey.KeyPressed +=new EventHandler<KeyPressedEventArgs>(PlainHotKey_KeyPressed);
			htmlHotKey.KeyPressed +=new EventHandler<KeyPressedEventArgs>(HtmlHotKey_KeyPressed);
			ConfigureApplication();
		}
		
		/// <summary>
		/// Creates the context menu on the right click of the tray icon.
		/// </summary>
		/// <returns>a list of MenuItems to display</returns>
		private MenuItem[] InitializeMenu()
		{
			MenuItem mnuConvert = new MenuItem("Convert To Text", IconDoubleClick);
			mnuConvert.DefaultItem = true;
			MenuItem[] menu = new MenuItem[] {
				mnuConvert,
				new MenuItem("Options... ", menuOptionsClick),
				new MenuItem("About "+Preferences.APPLICATION_TITLE+"...", menuAboutClick),
				new MenuItem("-"),
				new MenuItem("Exit", menuExitClick)
			};
			return menu;
		}
		
		/// <summary>
		/// Configures the Hotkey based on preferences.
		/// </summary>
		private void ConfigureApplication() {
			try {
				ModifierKeys modifier = ModifierKeys.None;
				if (Preferences.Instance.ModifierAlt) {
					modifier = modifier | ModifierKeys.Alt;
				}
				if (Preferences.Instance.ModifierControl) {
					modifier = modifier | ModifierKeys.Control;
				}
				if (Preferences.Instance.ModifierShift) {
					modifier = modifier | ModifierKeys.Shift;
				}
				if (Preferences.Instance.ModifierWindows) {
					modifier = modifier | ModifierKeys.Win;
				}
				
				// remove current hotkeys
				hotkey.UnregisterHotKeys();
				
				// get the new hotkey
				KeysConverter keysConverter = new KeysConverter();
				Keys keys = (Keys)keysConverter.ConvertFromString(Preferences.Instance.Hotkey);
				Keys plainKey = (Keys)keysConverter.ConvertFromString(Preferences.Instance.PlainTextHotKey);
				Keys htmlKey = (Keys)keysConverter.ConvertFromString(Preferences.Instance.HtmlTextHotKey);
				
				// register the control combination as hot key.
				hotkey.RegisterHotKey(modifier, keys);
				plainHotKey.RegisterHotKey(modifier, plainKey);
				htmlHotKey.RegisterHotKey(modifier, htmlKey);
				
				// set the visibility of the icon
				this.notifyIcon.Visible = Preferences.Instance.TrayIconVisible;
			} catch (Exception) {
				// could not register hotkey!
			}
		}
		#endregion
		
		#region Main - Program entry point
		/// <summary>Program entry point.</summary>
		/// <param name="args">Command Line Arguments</param>
		[STAThread]
		public static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			
			bool isFirstInstance;
			// Please use a unique name for the mutex to prevent conflicts with other programs
			using (Mutex mtx = new Mutex(true, Preferences.APPLICATION_TITLE, out isFirstInstance))
			{
				if (isFirstInstance) 
				{
					NotificationIcon notificationIcon = new NotificationIcon();
					
					Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
					AssemblyName asmName = assembly.GetName();
					notificationIcon.notifyIcon.Text  = String.Format("{0} {1}", Preferences.APPLICATION_TITLE, asmName.Version );

					Application.Run();
					notificationIcon.notifyIcon.Dispose();
				} 
				else 
				{
					// The application is already running
				}
			} // releases the Mutex
		}
		#endregion
		
		#region Event Handlers
		private void menuAboutClick(object sender, EventArgs e)
		{
			FormAbout frmAbout = new FormAbout();
			frmAbout.ShowDialog();
		}
		
		private void menuOptionsClick(object sender, EventArgs e)
		{
			FormOptions frmOptions = new FormOptions();
			if (frmOptions.ShowDialog() == DialogResult.OK) {
				ConfigureApplication();
			}
		}
		
		private void menuExitClick(object sender, EventArgs e)
		{
			Application.Exit();
		}
		
		private void IconDoubleClick(object sender, EventArgs e)
		{
			// put plain text on the clipboard replacing anything that was there
			string plainText = Clipboard.GetText(TextDataFormat.UnicodeText);
			if (String.Empty.Equals(plainText)) {
				return;
			}
			
			// put plain text on the clipboard
			Clipboard.SetText(plainText, TextDataFormat.UnicodeText);
		}
		
		/// <summary>
		/// When the hotkey combo is pressed do the following:
		/// 1. Make the data plain text and put it on the clipboard
		/// 2. Send CTRL+V to Paste in the current foreground application
		/// </summary>
		/// <param name="sender">the sending object</param>
		/// <param name="e">the event of which key was pressed</param>
		void Hotkey_KeyPressed(object sender, KeyPressedEventArgs e)
		{
			// get the text and exit if no text on clipboard
			string plainText = Clipboard.GetText(TextDataFormat.UnicodeText);
			if (String.Empty.Equals(plainText)) {
				return;
			}
			
			// put plain text on the clipboard
			Clipboard.SetText(plainText, TextDataFormat.UnicodeText);
			
			if (Preferences.Instance.PasteIntoActiveWindow) {
				// send CTRL+V for Paste to the active window or control
				InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
			}

			// play a sound if the user wants to on every paste
			if (Preferences.Instance.PlaySound) {
				SystemSounds.Asterisk.Play();
			}
		}

		void PlainHotKey_KeyPressed(object sender, KeyPressedEventArgs e)
		{
			CleanText cleanText = new CleanText();

			// get the text and exit if no text on clipboard
			string plainText = Clipboard.GetText();
			if (String.Empty.Equals(plainText))
			{
				return;
			}

			// put plain text on the clipboard
			Clipboard.SetText(cleanText.ToPlain(plainText));

			if (Preferences.Instance.PasteIntoActiveWindow)
			{
				// send CTRL+V for Paste to the active window or control
				InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
			}

			// play a sound if the user wa nts to on every paste
			if (Preferences.Instance.PlaySound)
			{
				SystemSounds.Asterisk.Play();
			}
		}

		void HtmlHotKey_KeyPressed(object sender, KeyPressedEventArgs e)
		{
			CleanText cleanText = new CleanText();

			// get the text and exit if no text on clipboard
			string htmlText = Clipboard.GetText();
			if (String.Empty.Equals(htmlText))
			{
				return;
			}

			// put plain text on the clipboard
			Clipboard.SetText(cleanText.ToHtml(htmlText));

			if (Preferences.Instance.PasteIntoActiveWindow)
			{
				// send CTRL+V for Paste to the active window or control
				InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
			}

			// play a sound if the user wa nts to on every paste
			if (Preferences.Instance.PlaySound)
			{
				SystemSounds.Asterisk.Play();
			}
		}
		#endregion
	}
}
