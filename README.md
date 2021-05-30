<h1>Redirect Manager 3</h1>

<p>Redirect Manager is simple, portable, extendable, open source redirect tool for Optimizely/Episerver projects.</p>

<h3>Version 3?</h3>
<p>Original RedirectManager (versions 1 and 2) remains in <a href="https://github.com/huilaaja/RedirectManager">https://github.com/huilaaja/RedirectManager</a>. But version 3 contains so many breaking changes that it has own repo.</p>



<h2>Version 3.0 improvements</h2>
<p>Preview:</p>
<p><img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-1.png" /></p>
<p><i>Yes, it could be much prettier, but it does the job ;)</i></p>

<h2>NEW Features</h2>
<ul>
	<li>Automatic <b>conversion to internal links</b>.<br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-4.png" width="500" />
	</li>
	<li><b>Version history</b>, change tracking and latest changes view.<br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-9.png" width="500" />
	</li>
	<li><b>Export and import</b> CSV/Excel.<br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-8.png" width="500" />
	</li>
	<li><b>Relative to from url</b> -feature.<br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-15.png" width="500" />
	</li>
	<li>Automatic <b>host segregation</b> from urls.<br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-10.png" width="500" />
	</li>
	<li><b>Host filtering</b><br/>
	<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-6.png" width="500" />
	</li>
	<li>Multiple redirect rule <b>validations</b> and <b>error notifications</b>.
		<ul>
			<li>Content exists in the From Url<br/>
			<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-11.png" width="500" /></li>
		</ul>
	</li>
	<li><b>Commenting and logs</b> for each rule
		<ul>
			<li>Editors can add comments.<br/>
			<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-12.png" width="500" />
			</li>
			<li>Comment field will gather logs of automatic changes <i>(example trimming of end slash or convertion to internal link)</i><br/>
			<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-16.png" width="500" /></li>
		</ul>
	</li>
	<li><b>New UI</b> and improved UX.<br/><img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-1.png" width="500" />
		<ul>
			<li>More responsive and mobile friendly list view.</li>
			<li>Default host filter is current host.</li>
			<li>Long URL-addresses ending is hidden to fit the screen.<br/>
				<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-14.png" width="500" />
			</li>
			<li>Rule editing contains more instructions and visual clues<br/>
				<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-7.png" width="500" />
			</li>
		</ul>
	</li>
	<li>Security improvements
		<ul>
			<li>All the requests' data goes by POST method body and not by GET methods.</li>
			<li>Config-based access right limitation is switch to Controller-based limitations.</li>
		</ul>
	</li>
	<li>Improved multi-site support.
		<ul>
			<li>Any host (*) rules are visible even if host filter is selected.<br/>
			<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-5.png" width="500" /></li>
			<li>Default host filter is your current site host.</li>
			<li>Editors can add hosts which does not even need to exist in Optimizely host settings. This is practical when you are moving existing site to Optimizely but haven't set up the hosts and DNS-addresses yet.<br/>
			<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-7.png" width="500" /></li>
		</ul>
	</li>
	<li>Migration path from version 2 to version 3.<br/>
		<img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-17.png" width="500" />
	</li>
</ul>

<h2>Old Features</h2>
<ul>
	<li>All redirects are HTTP 301 (Moved permanently), because search engines only follow this kind of redirects.</li>
	<li>Allow moving and changing URLs of Episerver pages and the redirects still works.</li>
	<li>Easily create redirects to any URLs or to Episerver pages.</li>
	<li>Wild card rules.</li>
	<li>Reordering and prioritizing rules.</li>
	<li>And the most important: It's open-source and it's yours to extend and manipulate! <a href="https://github.com/solita" target="_blank">Solita &lt;3 open-source!</a></li>
</ul>

<h2>Minimum Requirements</h2>
<ul>
	<li>Optimizely/Episerver project.</li>
	<li>Entity Framework</li>
</ul>

<h2>Installation instructions</h2>
<ol>
	<li>Install Entity Framework from NuGet.<br/>
   https://www.nuget.org/packages/EntityFramework</li>
	<li>Copy files into your project & build</li>
	<li>Add .MapMvcAttributeRoutes() to RegisterRoutes override in Global.asax.cs</li>
	<li>Apply manually Web.Config transformation</li>
	<li>Go to www.yourproject.com/Admin/RedirectManager</li>
</ol>
<p><img src="https://raw.githubusercontent.com/huilaaja/RedirectManager3/master/images/redirect-manager3-18.png" width="500" /></p>



<h2>Basic 404 redirect logic</h2>
<p><img src="https://raw.githubusercontent.com/huilaaja/RedirectManager/master/images/redirect-manager-5.png" /></p>
